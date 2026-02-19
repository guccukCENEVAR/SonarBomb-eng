using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Cvars;
using RayTrace; 
using System.Drawing;

namespace SonarBomb;

public class SonarBomb : BasePlugin
{
    public override string ModuleName => "SonarBomb";
    public override string ModuleVersion => "1.0.2";
    public override string ModuleAuthor => "guccukCENEVAR";
    public override string ModuleDescription => "Tactical Sonar Grenade for Hide and Seek.";

    private bool _isPluginActive = false;
    private float _originalBuyTime = 9999.0f; 

    private const float SCAN_RADIUS = 3000.0f;
    private const string BLINK_SOUND = "sounds/ui/csgo_ui_button_rollover_large.vsnd";
    private const string SONAR_EFFECT = "particles/explosions_fx/explosion_decoy.vpcf";

    public override void Load(bool hotReload)
    {
        // Initialize the RayTrace module
        RayTrace.RayTrace.Initialize(); 
        
        RegisterEventHandler<EventDecoyStarted>(OnDecoyStarted);
        
        AddCommandListener("say", OnPlayerChat);
        AddCommandListener("say_team", OnPlayerChat);

        // Ensure the market is open when the plugin is loaded
        Server.ExecuteCommand("mp_buytime 9999");
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

        // !sonarbomb: Toggle Plugin State
        if (command == "!sonarbomb")
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
            {
                TogglePluginState(player);
            }
            else
            {
                player.PrintToChat(" \x02[SonarBomb]\x01 You do not have permission.");
            }
            return HookResult.Handled;
        }

        // !sonar: Distribution Command
        if (command == "!sonar")
        {
            if (!AdminManager.PlayerHasPermissions(player, "@css/generic")) return HookResult.Handled;
            
            if (!_isPluginActive) 
            { 
                player.PrintToChat(" \x02[SonarBomb]\x01 Plugin is disabled. Type !sonarbomb to enable it."); 
                return HookResult.Handled; 
            }

            string targetArg = args.Length > 1 ? args[1] : "@me";
            DistributeSonar(player, targetArg);
            return HookResult.Handled;
        }
        return HookResult.Continue;
    }

    // ============================================================
    // 2. MARKET MANAGEMENT (BuyTime Logic)
    // ============================================================
    private void TogglePluginState(CCSPlayerController admin)
    {
        _isPluginActive = !_isPluginActive;
        
        if (_isPluginActive)
        {
            // Disable market (0 seconds)
            Server.ExecuteCommand("mp_buytime 0");
            admin.PrintToChat(" \x01[SonarBomb] Status: \x04ACTIVE \x01(Market Disabled)");
        }
        else
        {
            // Enable market (Unlimited)
            Server.ExecuteCommand("mp_buytime 9999");
            admin.PrintToChat(" \x01[SonarBomb] Status: \x02INACTIVE \x01(Market Enabled)");
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
            target.PrintToChat(" \x04[Sonar]\x01 A Sonar Grenade has been added to your inventory!");
        }
    }

    // ============================================================
    // 3. WALL & VISIBILITY CHECK (RayTrace Integrated)
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
        // Engine-level check via RayTrace.cs
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
    // 4. EFFECTS & LOGIC
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

        bool anyTargetFound = false;

        foreach (var target in Utilities.GetPlayers())
        {
            if (!target.IsValid || !target.PawnIsAlive || target.TeamNum == attacker.TeamNum) 
                continue;

            var pawn = target.PlayerPawn.Value;
            if (pawn == null || pawn.AbsOrigin == null) continue;

            // Calculate distance manually
            float distance = (decoyPos - pawn.AbsOrigin).Length();
            if (distance > SCAN_RADIUS) continue;

            if (IsTargetVisible(decoyPos, target, projectile.Handle))
            {
                anyTargetFound = true;
                break; 
            }
        }

        if (anyTargetFound)
        {
            attacker.ExecuteClientCommand($"play {BLINK_SOUND}");
        }

        // Kill the projectile for a silent effect
        projectile.AcceptInput("Kill");

        return HookResult.Continue;
    }
}