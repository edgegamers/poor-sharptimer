using System.Numerics;
using System.Runtime.CompilerServices;

public class CBaseUserCmd
{
    private const int ViewAnglesPointerOffset = 0x40;
    private const int ViewAnglesPitchOffset = 0x18;
    private const int ViewAnglesYawOffset = 0x1C;
    private const int ViewAnglesRollOffset = 0x20;
    private const int ForwardMoveOffset = 0x58;
    private const int SideMoveOffset = 0x5C;
    private const int MouseXOffset = 0x68;
    private const int MouseYOffset = 0x6C;

    public CBaseUserCmd(IntPtr pointer)
    {
        Handle = pointer;
    }

    public IntPtr Handle { get; set; }
    public float ForwardMove => GetForwardMove();
    public float SideMove => GetSideMove();
    public unsafe float GetForwardMove()
    {
        var ForwardMove = Unsafe.Read<float>((void*)(Handle + ForwardMoveOffset));
        return ForwardMove;
    }
    public unsafe float GetMouseX()
    {
        var MouseX = Unsafe.Read<float>((void*)(Handle + MouseXOffset));
        return MouseX;
    }
    public unsafe float GetMouseY()
    {
        var MouseY = Unsafe.Read<float>((void*)(Handle + MouseYOffset));
        return MouseY;
    }
    public unsafe float GetSideMove()
    {
        var SideMove = Unsafe.Read<float>((void*)(Handle + SideMoveOffset));
        return SideMove;
    }
    public unsafe void DisableSideMove()
    {
        if (Handle == IntPtr.Zero)
            return;

        Unsafe.Write<float>((void*)(Handle + SideMoveOffset), 0);
    }
    public unsafe void DisableForwardMove()
    {
        if (Handle == IntPtr.Zero)
            return;

        Unsafe.Write<float>((void*)(Handle + ForwardMoveOffset), 0);
    }
    public unsafe void ClearMouseInput()
    {
        if (Handle == IntPtr.Zero)
            return;

        Unsafe.Write<float>((void*)(Handle + MouseXOffset), 0);
        Unsafe.Write<float>((void*)(Handle + MouseYOffset), 0);
    }
    public unsafe bool SetViewAngles(Vector3 angles)
    {
        if (Handle == IntPtr.Zero)
            return false;

        var viewAngles = Unsafe.Read<IntPtr>((void*)(Handle + ViewAnglesPointerOffset));
        if (viewAngles == IntPtr.Zero)
            return false;

        Unsafe.Write<float>((void*)(viewAngles + ViewAnglesPitchOffset), angles.X);
        Unsafe.Write<float>((void*)(viewAngles + ViewAnglesYawOffset), angles.Y);
        Unsafe.Write<float>((void*)(viewAngles + ViewAnglesRollOffset), angles.Z);
        return true;
    }
}
