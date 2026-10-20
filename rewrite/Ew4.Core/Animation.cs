using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public enum AnimationMode { Loop, Once, Paused }

public sealed class AnimationLayer
{
    private readonly int[] keys;
    public int Duration { get; }
    public AnimationMode Mode { get; set; }
    public int FrameIndex { get; private set; }
    public int Tick { get; private set; }
    public AnimationLayer(IEnumerable<int> frameKeys, int duration, AnimationMode mode = AnimationMode.Loop)
    {
        keys = frameKeys.ToArray();
        if (duration <= 0 || keys.Length == 0 || keys[0] != 0 || keys[keys.Length - 1] >= duration
            || keys.Any(k => k < 0) || !keys.SequenceEqual(keys.OrderBy(k => k).Distinct()))
            throw new RuleViolation("animation.keys", "Animation keys must start at0, increase, and fit duration");
        Duration = duration; Mode = mode;
    }
    public void Seek(int tick)
    {
        if (tick < 0 || tick >= Duration) throw new RuleViolation("animation.tick", $"Tick outside clip: {tick}");
        Tick = tick;
        FrameIndex = 0;
        while (FrameIndex + 1 < keys.Length && keys[FrameIndex + 1] <= tick) FrameIndex++;
    }
    public void Advance()
    {
        if (Mode == AnimationMode.Paused || (Mode == AnimationMode.Once && FrameIndex == keys.Length - 1)) return;
        Tick++;
        if (FrameIndex == keys.Length - 1 && Tick >= Duration) { Tick = 0; FrameIndex = 0; }
        else if (FrameIndex + 1 < keys.Length && keys[FrameIndex + 1] <= Tick) FrameIndex++;
    }
}

public readonly record struct Affine2(float A, float B, float C, float D, float X, float Y)
{
    public static Affine2 Identity => new(1, 0, 0, 1, 0, 0);
    public static Affine2 Compose(Affine2 parent, Affine2 local) => new(
        parent.A * local.A + parent.C * local.B,
        parent.B * local.A + parent.D * local.B,
        parent.A * local.C + parent.C * local.D,
        parent.B * local.C + parent.D * local.D,
        parent.A * local.X + parent.C * local.Y + parent.X,
        parent.B * local.X + parent.D * local.Y + parent.Y);
    public (float X, float Y) Point(float x, float y) => (A * x + C * y + X, B * x + D * y + Y);
    public uint ModulateAlpha(uint color, float alpha)
    {
        if (float.IsNaN(alpha) || alpha < 0 || alpha > 1) throw new RuleViolation("animation.alpha", "Alpha outside0..1");
        return (color & 0xffffff) | ((uint)(alpha * (color >> 24)) << 24);
    }
}
