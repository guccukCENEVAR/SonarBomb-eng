using System.Numerics;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

// ============================================================
// FUNPLAY Ray-Trace Metamod Module C# Wrapper
// https://github.com/FUNPLAY-pro-CS2/Ray-Trace
// 
// This file connects to FUNPLAY's Metamod C++ module.
// Module performs engine level trace - NO signature scanning required.
// ============================================================

namespace RayTrace;

// ============================================================
// InteractionLayers - CS2 collision layers
// ============================================================
[Flags]
public enum InteractionLayers : ulong
{
    Solid                   = 0x1,
    Hitboxes                = 0x2,
    Trigger                 = 0x4,
    Sky                     = 0x8,
    PlayerClip              = 0x10,
    NPCClip                 = 0x20,
    BlockLOS                = 0x40,
    BlockLight              = 0x80,
    Ladder                  = 0x100,
    Pickup                  = 0x200,
    BlockSound              = 0x400,
    NoDraw                  = 0x800,
    Window                  = 0x1000,
    PassBullets             = 0x2000,
    WorldGeometry           = 0x4000,
    Water                   = 0x8000,
    Slime                   = 0x10000,
    TouchAll                = 0x20000,
    Player                  = 0x40000,
    NPC                     = 0x80000,
    Debris                  = 0x100000,
    Physics_Prop            = 0x200000,
    NavIgnore               = 0x400000,
    NavLocalIgnore          = 0x800000,
    PostProcessingVolume    = 0x1000000,
    CarriedObject           = 0x4000000,
    PushAway                = 0x8000000,
    ServerEntityOnClient    = 0x10000000,
    CarriedWeapon           = 0x20000000,
    StaticLevel             = 0x40000000,
    csgo_team1              = 0x80000000,
    csgo_team2              = 0x100000000,
    csgo_grenadeclip        = 0x200000000,
    csgo_droneclip          = 0x400000000,
    csgo_moveable           = 0x800000000,
    csgo_opaque             = 0x1000000000,
    csgo_monster            = 0x2000000000,
    csgo_thrown_grenade     = 0x8000000000,

    // Ready-made masks (compatible with official FUNPLAY example)
    MASK_SHOT_PHYSICS = Solid | PlayerClip | Window | PassBullets | Player | NPC | Physics_Prop,
    MASK_SHOT_HITBOX  = Hitboxes | Player | NPC,
    MASK_SHOT_FULL    = MASK_SHOT_PHYSICS | Hitboxes,
    MASK_WORLD_ONLY   = Solid | Window | PassBullets,
    MASK_GRENADE      = Solid | Window | Physics_Prop | PassBullets,
    MASK_BRUSH_ONLY   = Solid | Window,
    MASK_PLAYER_MOVE  = Solid | Window | PlayerClip | PassBullets,
    MASK_NPC_MOVE     = Solid | Window | NPCClip | PassBullets,

    // Wall check: MASK_SHOT_PHYSICS is used (Windows/Linux compatible)
    // Player/NPC filtering is done in C# SingleTrace, not InteractsExclude
    MASK_WALL_CHECK   = MASK_SHOT_PHYSICS,
}

// ============================================================
// TraceOptions - Trace options (compatible with C++ struct)
// ============================================================
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct TraceOptions
{
    [FieldOffset(0)]  public ulong InteractsWith;
    [FieldOffset(8)]  public ulong InteractsExclude;
    [FieldOffset(16)] public int   DrawBeam;

    /// Wall check: detects everything with MASK_SHOT_PHYSICS
    /// Player/entity filtering is done on C# side (SingleTrace)
    /// This approach is compatible with the official FUNPLAY example and works on Windows/Linux
    public static TraceOptions WallCheck => new()
    {
        InteractsWith = (ulong)InteractionLayers.MASK_SHOT_PHYSICS,
        InteractsExclude = 0,
        DrawBeam = 0
    };

    /// Wall check + debug beam (for tracetest command)
    public static TraceOptions WallCheckDebug => new()
    {
        InteractsWith = (ulong)InteractionLayers.MASK_SHOT_PHYSICS,
        InteractsExclude = 0,
        DrawBeam = 1
    };

    /// Full bullet trace (including hitbox)
    public static TraceOptions ShotFull => new()
    {
        InteractsWith = (ulong)InteractionLayers.MASK_SHOT_FULL,
        InteractsExclude = 0,
        DrawBeam = 0
    };
}

// ============================================================
// TraceResult - Trace result (compatible with C++ struct)
// ============================================================
[StructLayout(LayoutKind.Explicit, Size = 44)]
public struct TraceResult
{
    [FieldOffset(0)]  public float EndPosX;
    [FieldOffset(4)]  public float EndPosY;
    [FieldOffset(8)]  public float EndPosZ;
    [FieldOffset(16)] public nint  HitEntity;
    [FieldOffset(24)] public float Fraction;
    [FieldOffset(28)] public int   AllSolid;
    [FieldOffset(32)] public float NormalX;
    [FieldOffset(36)] public float NormalY;
    [FieldOffset(40)] public float NormalZ;

    public bool DidHit => Fraction < 1.0f;
    public bool IsAllSolid => AllSolid != 0;
}

// ============================================================
// CRayTrace - Main trace class (Connects to Metamod module)
// ============================================================
public static class RayTrace
{
    private static nint _handle = nint.Zero;
    private static bool _loaded = false;

    // VTable function delegates
    private static Func<nint, nint, nint, nint, nint, nint, bool>? _traceShape;
    private static Func<nint, nint, nint, nint, nint, nint, bool>? _traceEndShape;
    private static Func<nint, nint, nint, nint, nint, nint, nint, nint, bool>? _traceHullShape;

    public static bool IsInitialized => _loaded;
    public static string? InitError { get; private set; }
    private static int _initRetryCount = 0;
    private static bool _gaveUp = false;

    /// <summary>
    /// Connect to FUNPLAY Ray-Trace Metamod module.
    /// Lazy init: called automatically on OnTick and commands.
    /// No limit - tries silently until connected.
    /// </summary>
    public static void Initialize()
    {
        if (_loaded) return;
        if (_gaveUp) return;

        _initRetryCount++;

        try
        {
            object? factory = null;
            
            try
            {
                factory = Utilities.MetaFactory("CRayTraceInterface002");
            }
            catch (Exception ex)
            {
                _gaveUp = true;
                InitError = $"MetaFactory exception: {ex.Message}";
                return;
            }

            if (factory == null)
            {
                InitError = $"MetaFactory null (attempt {_initRetryCount})";
                return;
            }

            _handle = (nint)factory;

            if (_handle == nint.Zero)
            {
                _gaveUp = true;
                InitError = "CRayTraceInterface002 handle is zero.";
                return;
            }

            BindVTable();
            _loaded = true;
            InitError = null;
        }
        catch (Exception ex)
        {
            _loaded = false;
            _handle = nint.Zero;
            InitError = ex.Message;
            _gaveUp = true;
        }
    }

    private static void BindVTable()
    {
        // VTable indexes vary by platform (C++ ABI difference)
        int traceShapeIdx, traceEndShapeIdx, traceHullShapeIdx;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            traceShapeIdx = 1;
            traceEndShapeIdx = 2;
            traceHullShapeIdx = 3;
        }
        else // Linux (Itanium ABI)
        {
            traceShapeIdx = 2;
            traceEndShapeIdx = 3;
            traceHullShapeIdx = 4;
        }

        _traceShape = VirtualFunction.Create<nint, nint, nint, nint, nint, nint, bool>(_handle, traceShapeIdx);
        _traceEndShape = VirtualFunction.Create<nint, nint, nint, nint, nint, nint, bool>(_handle, traceEndShapeIdx);
        _traceHullShape = VirtualFunction.Create<nint, nint, nint, nint, nint, nint, nint, nint, bool>(_handle, traceHullShapeIdx);
    }

    /// <summary>
    /// Casts a ray trace from start to end point.
    /// </summary>
    public static unsafe bool TraceEndShape(
        Vector origin, Vector endOrigin, 
        CBaseEntity? ignoreEntity, 
        TraceOptions options, 
        out TraceResult result)
    {
        result = default;
        if (!_loaded) Initialize();
        if (!_loaded || _traceEndShape == null) return false;

        TraceResult resultBuffer = default;
        TraceOptions optionsBuffer = options;

        bool success = _traceEndShape(
            _handle,
            origin.Handle,
            endOrigin.Handle,
            ignoreEntity?.Handle ?? nint.Zero,
            (nint)(&optionsBuffer),
            (nint)(&resultBuffer));

        result = resultBuffer;
        return success;
    }

    /// <summary>
    /// Angle-based trace (forward vector is calculated automatically, 8192 units distance).
    /// </summary>
    public static unsafe bool TraceShape(
        Vector origin, QAngle angles,
        CBaseEntity? ignoreEntity,
        TraceOptions options,
        out TraceResult result)
    {
        result = default;
        if (!_loaded) Initialize();
        if (!_loaded || _traceShape == null) return false;

        TraceResult resultBuffer = default;
        TraceOptions optionsBuffer = options;

        bool success = _traceShape(
            _handle,
            origin.Handle,
            angles.Handle,
            ignoreEntity?.Handle ?? nint.Zero,
            (nint)(&optionsBuffer),
            (nint)(&resultBuffer));

        result = resultBuffer;
        return success;
    }

    // ============================================================
    // Aimbot compatible API (Functions used by Aimbot.cs)
    // ============================================================

    /// <summary>
    /// Wall check trace. Only detects real world walls.
    /// Players, triggers, spawn barriers, glass, buyzones → pass through.
    /// 
    /// return: TraceResult (Fraction &lt; 0.97 = wall exists)
    /// </summary>
    public static bool TraceWall(Vector start, Vector end, IntPtr skipHandle, out TraceResult result)
    {
        result = default;
        
        // Lazy init: try again if module not connected yet
        if (!_loaded) Initialize();
        if (!_loaded) return false;

        CBaseEntity? skipEntity = null;
        if (skipHandle != IntPtr.Zero)
        {
            try { skipEntity = new CBaseEntity(skipHandle); }
            catch { }
        }

        var options = TraceOptions.WallCheck;
        return TraceEndShape(start, end, skipEntity, options, out result);
    }

    /// <summary>
    /// Debug version - draws beam (for tracetest command)
    /// </summary>
    public static bool TraceWallDebug(Vector start, Vector end, IntPtr skipHandle, out TraceResult result)
    {
        result = default;
        
        if (!_loaded) Initialize();
        if (!_loaded) return false;

        CBaseEntity? skipEntity = null;
        if (skipHandle != IntPtr.Zero)
        {
            try { skipEntity = new CBaseEntity(skipHandle); }
            catch { }
        }

        var options = TraceOptions.WallCheckDebug;
        return TraceEndShape(start, end, skipEntity, options, out result);
    }
}
