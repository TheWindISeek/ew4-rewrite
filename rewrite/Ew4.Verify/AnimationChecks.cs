using System.Text.Json;
using Ew4.Core;

internal static class AnimationChecks
{
    public static int Run(string root)
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/animation_vectors.json")));
        int count = 0;
        foreach (var vector in data.RootElement.GetProperty("timing").EnumerateArray())
        {
            var layer = new AnimationLayer([0, 4, 8], 12, (AnimationMode)vector.GetProperty("mode").GetInt32());
            layer.Seek(vector.GetProperty("start").GetInt32());
            bool first = true;
            foreach (var step in vector.GetProperty("expected").EnumerateArray())
            {
                if (!first) layer.Advance();
                if (layer.FrameIndex != step[0].GetInt32() || layer.Tick != step[1].GetInt32())
                    throw new InvalidOperationException("Native animation timing");
                first = false;
                count++;
            }
        }
        Affine2 Matrix(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle(), e[3].GetSingle(), e[4].GetSingle(), e[5].GetSingle());
        foreach (var vector in data.RootElement.GetProperty("transforms").EnumerateArray())
        {
            var composed = Affine2.Compose(Matrix(vector.GetProperty("parent")), Matrix(vector.GetProperty("local")));
            var expected = Matrix(vector.GetProperty("expected"));
            float[] actualFields = [composed.A, composed.B, composed.C, composed.D, composed.X, composed.Y];
            float[] expectedFields = [expected.A, expected.B, expected.C, expected.D, expected.X, expected.Y];
            // ARM64 fuses some multiply/add instructions. Screen positions use a tolerance.
            if (actualFields.Zip(expectedFields).Any(p => Math.Abs(p.First - p.Second) > .0001f)
                || composed.ModulateAlpha(0xc0334455, vector.GetProperty("alpha").GetSingle()) != vector.GetProperty("color").GetUInt32())
                throw new InvalidOperationException("Native animation transform/color");
            count++;
        }
        return count;
    }
}
