using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Cvars;
using RayTrace;
using System.Drawing;
using CounterStrikeSharp.API.Modules.Timers;

namespace SonarBomb;

public class SonarBomb : BasePlugin
{
    public override string ModuleName => "SonarBomb";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "guccukCENEVAR";
    public override string ModuleDescription => "Tick-based glow effect with admin controlled Sonar Bomb.";

    private bool _isPluginActive = false;

    private const float SCAN_RADIUS = 3000.0f;
    private const string SONAR_EFFECT = "particles/explosions_fx/explosion_decoy.vpcf";
    private const float GLOW_DURATION = 7.0f; // 7 seconds glow effect

    // Glow + Relay pair stored together (glow disappears without relay)
    private class GlowEntry
    {
        public CDynamicProp? Relay;
        public CDynamicProp? Glow;
        public DateTime SpawnedAt;
    }

    private List<GlowEntry> glowTimers = new();

    public override void Load(bool hotReload)
    {
        // Initialize RayTrace module
        RayTrace.RayTrace.Initialize();

        RegisterEventHandler<EventDecoyStarted>(OnDecoyStarted);

        AddCommandListener("say", OnPlayerChat);
        AddCommandListener("say_team", OnPlayerChat);

        // Glow duration check timer (check every 0.1 seconds)
        AddTimer(0.1f, () => CheckGlowTimers(), TimerFlags.REPEAT);
    }

    private void CheckGlowTimers()
    {
        var now = DateTime.Now;
        // Collect expired OR invalid entries
        var expired = glowTimers
            .Where(e => (now - e.SpawnedAt).TotalSeconds >= GLOW_DURATION
                        || (e.Glow == null || !e.Glow.IsValid)
                        || (e.Relay == null || !e.Relay.IsValid))
            .ToList();

        foreach (var entry in expired)
        {
            try
            {
                if (entry.Glow != null && entry.Glow.IsValid)
                    entry.Glow.Remove();
                if (entry.Relay != null && entry.Relay.IsValid)
                    entry.Relay.Remove();
            }
            catch { /* silent */ }

            glowTimers.Remove(entry);

            if ((now - entry.SpawnedAt).TotalSeconds >= GLOW_DURATION)
                ConsolePrintln($"[SonarBomb] Glow removed (time expired)");
        }
    }

    // ============================================================
    // 1. COMMANDS (Admin Only)
    // ============================================================
    [ConsoleCommand("css_sonar", "Gives SonarBomb to players.")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnSonarConsoleCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && !AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat(" \x02[SonarBomb]\x01 You do not have permission.");
            return;
        }
        string targetArg = command.ArgCount > 1 ? command.GetArg(1) : "@me";
        DistributeSonar(player, targetArg);
    }

    private HookResult OnPlayerChat(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return HookResult.Continue;
        string msg = info.GetArg(1).Trim();
        string[] args = msg.Split(' ');
        string command = args[0].ToLower();

        if (command == "!sonarbomb")
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                TogglePluginState(player);
            }
            return HookResult.Handled;
        }

        if (command == "!sonar")
        {
            if (!AdminManager.PlayerHasPermissions(player, "@css/generic")) return HookResult.Handled;
            if (!_isPluginActive) { player.PrintToChat(" \x02[SonarBomb]\x01 Plugin is disabled. Type !sonarbomb first."); return HookResult.Handled; }

            string targetArg = args.Length > 1 ? args[1] : "@me";
            DistributeSonar(player, targetArg);
            return HookResult.Handled;
        }
        return HookResult.Continue;
    }

    // ============================================================
    // 2. PLUGIN STATE MANAGEMENT
    // ============================================================
    private void TogglePluginState(CCSPlayerController admin)
    {
        _isPluginActive = !_isPluginActive;

        if (_isPluginActive)
        {
            admin.PrintToChat(" \x01[SonarBomb] Status: \x04ACTIVE");
        }
        else
        {
            // Clear all glow effects when plugin is disabled
            CleanupAllGlowEffects();
            admin.PrintToChat(" \x01[SonarBomb] Status: \x02INACTIVE \x01(Glow effects cleared)");
        }
    }

    private void CleanupAllGlowEffects()
    {
        try
        {
            // Clear all glow and relay entities
            foreach (var entry in glowTimers.ToList())
            {
                if (entry.Glow != null && entry.Glow.IsValid)
                    entry.Glow.Remove();
                if (entry.Relay != null && entry.Relay.IsValid)
                    entry.Relay.Remove();
            }
            glowTimers.Clear();
        }
        catch
        {
            // Silent fail
        }
    }

    private void DistributeSonar(CCSPlayerController? issuer, string targetArg)
    {
        List<CCSPlayerController> targetList = new();
        var allPlayers = Utilities.GetPlayers();
        targetArg = targetArg.ToLower();

        switch (targetArg)
        {
            case "@all": targetList = allPlayers.Where(p => p.IsValid && p.PawnIsAlive).ToList(); break;
            case "@t": targetList = allPlayers.Where(p => p.IsValid && p.PawnIsAlive && p.TeamNum == (byte)CsTeam.Terrorist).ToList(); break;
            case "@ct": targetList = allPlayers.Where(p => p.IsValid && p.PawnIsAlive && p.TeamNum == (byte)CsTeam.CounterTerrorist).ToList(); break;
            case "@me": if (issuer != null && issuer.IsValid && issuer.PawnIsAlive) targetList.Add(issuer); break;
            default:
                var found = allPlayers.FirstOrDefault(p => p.PlayerName.ToLower().Contains(targetArg));
                if (found != null && found.IsValid && found.PawnIsAlive) targetList.Add(found);
                break;
        }

        foreach (var target in targetList)
        {
            target.GiveNamedItem("weapon_decoy");
            target.PrintToChat(" \x04[Sonar]\x01 A Sonar Bomb has been added to your inventory!");
        }
    }

    // ============================================================
    // 3. GLOW EFFECT (Tick-based, prop_dynamic based ESP glow)
    // ============================================================
    private void ApplyGlowToPlayer(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
            return;

        var pawn = player.PlayerPawn.Value;
        if (!pawn.IsValid) return;

        try
        {
            // Create ESP-Players style glow entity
            CreateGlowEntity(player, pawn);
        }
        catch
        {
            // Silent fail - pawn might be disconnected
        }
    }

    private void CreateGlowEntity(CCSPlayerController player, CCSPlayerPawn pawn)
    {
        try
        {
            // Get model name from player
            string modelName = pawn.CBodyComponent?.SceneNode?.GetSkeletonInstance().ModelState.ModelName ?? "";
            if (string.IsNullOrEmpty(modelName)) return;

            // 1) RELAY entity (invisible carrier) - pawn follows this
            var relay = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            if (relay == null) return;

            relay.DispatchSpawn();
            relay.SetModel(modelName);
            relay.Spawnflags = 256u;
            relay.RenderMode = RenderMode_t.kRenderNone; // Completely invisible

            // 2) GLOW entity (actual glow emitter) - follows relay
            var glow = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            if (glow == null)
            {
                if (relay.IsValid) relay.Remove();
                return;
            }

            glow.Render = Color.FromArgb(1, 0, 0, 0); // Minimal alpha (only glow is visible)
            glow.DispatchSpawn();
            glow.SetModel(modelName);
            glow.Spawnflags = 256u;

            // Set glow properties - Based on team color
            Color glowColor = player.TeamNum == (byte)CsTeam.Terrorist
                ? Color.Red      // Terrorists red
                : Color.Cyan;    // CTs blue

            glow.Glow.GlowColorOverride = glowColor;
            glow.Glow.GlowRange = 2000;        // 2000 units distance
            glow.Glow.GlowTeam = -1;           // All teams can see
            glow.Glow.GlowType = 3;            // Glow type (visible through walls)
            glow.Glow.GlowRangeMin = 100;      // No glow when very close

            // Follow chain: Relay -> Pawn,  Glow -> Relay
            relay.AcceptInput("FollowEntity", pawn, relay, "!activator");
            glow.AcceptInput("FollowEntity", relay, glow, "!activator");

            // Add to list (for automatic cleanup)
            glowTimers.Add(new GlowEntry
            {
                Relay = relay,
                Glow = glow,
                SpawnedAt = DateTime.Now
            });

            ConsolePrintln($"[SonarBomb] Glow created: {player.PlayerName} - {GLOW_DURATION} seconds");
        }
        catch (Exception ex)
        {
            ConsolePrintln($"[SonarBomb] Glow error: {ex.Message}");
        }
    }

    private void ConsolePrintln(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    // ============================================================
    // 4. WALL & VISIBILITY CHECK (RayTrace Integrated)
    // ============================================================
    private static bool IsWallEntity(string name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        if (name.StartsWith("world", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("func_wall", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("func_brush", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("func_breakable", StringComparison.OrdinalIgnoreCase)) return true;
        if (name.StartsWith("prop_static", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private bool SingleTrace(Vector from, Vector to, IntPtr skipHandle)
    {
        // Check at engine level via RayTrace.cs
        if (!RayTrace.RayTrace.TraceWall(from, to, skipHandle, out RayTrace.TraceResult result)) return false;
        if (result.IsAllSolid) return false;
        if (result.Fraction >= 0.97f) return false;
        if (result.HitEntity == nint.Zero) return true;

        try
        {
            var hitEnt = new CEntityInstance(result.HitEntity);
            if (hitEnt == null || !hitEnt.IsValid) return false;
            if (IsWallEntity(hitEnt.DesignerName ?? "")) return true;
            return false;
        }
        catch { return false; }
    }

    private bool IsTargetVisible(Vector sonarPos, CCSPlayerController target, IntPtr sonarHandle)
    {
        var pawn = target.PlayerPawn.Value;
        if (pawn == null || pawn.AbsOrigin == null) return false;

        float eyeZ = (pawn.ViewOffset != null) ? pawn.ViewOffset.Z : 64.0f;
        if (eyeZ < 30.0f) eyeZ = 64.0f;

        float tx = pawn.AbsOrigin.X;
        float ty = pawn.AbsOrigin.Y;
        float tz = pawn.AbsOrigin.Z;

        // Multi-point check (Head, Chest, Waist)
        if (!SingleTrace(sonarPos, new Vector(tx, ty, tz + eyeZ), sonarHandle)) return true;
        if (!SingleTrace(sonarPos, new Vector(tx, ty, tz + eyeZ * 0.6f), sonarHandle)) return true;
        if (!SingleTrace(sonarPos, new Vector(tx, ty, tz + eyeZ * 0.35f), sonarHandle)) return true;

        return false;
    }

    // ============================================================
    // 5. EFFECT & GAME LOGIC
    // ============================================================

    private void SpawnSonarRing(Vector pos)
    {
        Vector effectPos = new Vector(pos.X, pos.Y, pos.Z + 15.0f);

        var particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
        if (particle != null && particle.IsValid)
        {
            particle.Teleport(effectPos, new QAngle(0, 0, 0), new Vector(0, 0, 0));
            particle.EffectName = SONAR_EFFECT;
            particle.StartActive = true;
            particle.DispatchSpawn();
            particle.AcceptInput("Start");

            AddTimer(2.0f, () =>
            {
                if (particle != null && particle.IsValid)
                    particle.AcceptInput("Kill");
            });
        }
    }

    private HookResult OnDecoyStarted(EventDecoyStarted @event, GameEventInfo info)
    {
        if (!_isPluginActive) return HookResult.Continue;

        var attacker = @event.Userid;
        if (attacker == null || !attacker.IsValid) return HookResult.Continue;

        var projectile = Utilities.GetEntityFromIndex<CBaseCSGrenadeProjectile>(@event.Entityid);
        if (projectile == null || !projectile.IsValid || projectile.AbsOrigin == null) return HookResult.Continue;

        Vector decoyPos = projectile.AbsOrigin;

        SpawnSonarRing(decoyPos);

        if (!RayTrace.RayTrace.IsInitialized) RayTrace.RayTrace.Initialize();

        // Collect detected players
        List<CCSPlayerController> detectedPlayers = new();

        foreach (var target in Utilities.GetPlayers())
        {
            if (!target.IsValid || !target.PawnIsAlive || target.TeamNum == attacker.TeamNum)
                continue;

            var pawn = target.PlayerPawn.Value;
            if (pawn == null || pawn.AbsOrigin == null) continue;

            float distance = (decoyPos - pawn.AbsOrigin).Length();
            if (distance > SCAN_RADIUS) continue;

            if (IsTargetVisible(decoyPos, target, projectile.Handle))
            {
                detectedPlayers.Add(target);
            }
        }

        // Give glow effect to ALL detected players
        foreach (var detectedPlayer in detectedPlayers)
        {
            ApplyGlowToPlayer(detectedPlayer);
        }

        projectile.AcceptInput("Kill");

        return HookResult.Continue;
    }
}
