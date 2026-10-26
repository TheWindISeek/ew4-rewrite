using System;
using System.Collections.Generic;
using System.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

/// <summary>
/// 开局时构建辖区（<see cref="TerritorialJurisdictions"/>）：自动按国家连通陆地划分，或按剧本手工指派。
/// 属于开局管线；Engine 只保留辖区数据与回合内的占领规则（<see cref="JurisdictionClaims"/>）。
/// </summary>
public static class TerritorialJurisdictionBuilder
{
    private static bool Land(CellState cell) => cell.Terrain.Active && !cell.Terrain.Sea && cell.BuildingKind != "port";

    // Partition each country's connected land before play. Sea never joins districts.
    public static TerritorialJurisdictions Create(WorldState state)
    {
        var map = new HexMap(state.Width, state.Height);
        var centers = Enumerable.Repeat(-1, state.Cells.Count).ToArray();
        var queue = new Queue<int>();
        for (int i = 0; i < centers.Length; i++)
            if (Land(state.Cells[i]) && TerritorialJurisdictions.IsCenter(state.Cells[i]) && state.Cells[i].Owner != null)
            { centers[i] = i; queue.Enqueue(i); }
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            for (int d = 0; d < 6; d++)
            {
                int next = map.Neighbor(cell, d);
                if (next < 0 || centers[next] >= 0 || !Land(state.Cells[next]) || state.Cells[next].Owner != state.Cells[cell].Owner) continue;
                centers[next] = centers[cell]; queue.Enqueue(next);
            }
        }
        var roots = centers.Distinct().Where(c => c >= 0).OrderBy(c => c).ToArray();
        var components = Enumerable.Repeat(-1, centers.Length).ToArray();
        for (int i = 0; i < centers.Length; i++)
        {
            if (!Land(state.Cells[i]) || components[i] >= 0) continue;
            components[i] = i; queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int next = map.Neighbor(cell, d);
                    if (next < 0 || !Land(state.Cells[next]) || components[next] >= 0) continue;
                    components[next] = i; queue.Enqueue(next);
                }
            }
        }
        // Islands/enclaves still have an administrative label, without creating a sea frontier.
        var detached = new HashSet<int>();
        for (int i = 0; i < centers.Length; i++)
        {
            if (!Land(state.Cells[i]) || centers[i] >= 0 || roots.Length == 0) continue;
            var distances = Enumerable.Repeat(-1, centers.Length).ToArray();
            distances[i] = 0; queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int next = map.Neighbor(cell, d);
                    if (next < 0 || !Land(state.Cells[next]) || distances[next] >= 0) continue;
                    distances[next] = distances[cell] + 1; queue.Enqueue(next);
                }
            }
            int nearest = roots.OrderBy(c => state.Cells[c].Owner == state.Cells[i].Owner ? 0 : 1)
                .ThenBy(c => distances[c] >= 0 ? 0 : 1).ThenBy(c => distances[c] >= 0 ? distances[c] : map.Distance(i, c)).ThenBy(c => c).First();
            centers[i] = nearest;
            if (components[nearest] != components[i] || state.Cells[nearest].Owner != state.Cells[i].Owner) detached.Add(i);
        }
        var borders = new HashSet<(int, int)>();
        for (int i = 0; i < centers.Length; i++)
        {
            if (centers[i] < 0 || state.Cells[i].Terrain.Sea || detached.Contains(i)) continue;
            for (int d = 0; d < 6; d++)
            {
                int next = map.Neighbor(i, d);
                if (next < 0 || state.Cells[next].Terrain.Sea || centers[next] < 0 || centers[next] == centers[i] || detached.Contains(next)) continue;
                borders.Add((Math.Min(centers[i], centers[next]), Math.Max(centers[i], centers[next])));
            }
        }
        return new() { CenterCaptureVersion = 2, CellCenters = centers, DetachedCells = detached.OrderBy(c => c).ToArray(), Borders = borders.OrderBy(b => b.Item1).ThenBy(b => b.Item2)
            .Select(b => new JurisdictionBorder(b.Item1, b.Item2)).ToArray() };
    }

    public static TerritorialJurisdictions FromAssignments(WorldState state, IReadOnlyList<int> assignments)
    {
        if (assignments.Count != state.Cells.Count) throw new RuleViolation("territory.jurisdictions", "手工辖区格子数量与地图不符");
        var centers = assignments.ToArray();
        for (int i = 0; i < centers.Length; i++)
        {
            int center = centers[i];
            if (center < -1 || center >= centers.Length || !Land(state.Cells[i]) && center != -1
                || Land(state.Cells[i]) && TerritorialJurisdictions.IsCenter(state.Cells[i]) && center != i
                || center >= 0 && (!Land(state.Cells[center]) || !TerritorialJurisdictions.IsCenter(state.Cells[center]) || centers[center] != center))
                throw new RuleViolation("territory.jurisdiction_cell", $"地块{i}的手工辖区中心无效: {center}；中心须为有效城市/工厂/马厩并指向自身，海格/港口/区域外须为-1");
        }
        // Administrative islands may share a label, but must not create a sea frontier.
        var map = new HexMap(state.Width, state.Height);
        var components = Enumerable.Repeat(-1, centers.Length).ToArray();
        var queue = new Queue<int>();
        for (int i = 0; i < centers.Length; i++)
        {
            if (!Land(state.Cells[i]) || components[i] >= 0) continue;
            components[i] = i;queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int next = map.Neighbor(cell, d);
                    if (next < 0 || !Land(state.Cells[next]) || components[next] >= 0) continue;
                    components[next] = i;queue.Enqueue(next);
                }
            }
        }
        var detached = Enumerable.Range(0, centers.Length).Where(i => centers[i] >= 0 && components[i] != components[centers[i]]).ToHashSet();
        var borders = new HashSet<(int, int)>();
        for (int i = 0; i < centers.Length; i++)
        {
            if (centers[i] < 0 || detached.Contains(i)) continue;
            for (int d = 0; d < 6; d++)
            {
                int next = map.Neighbor(i, d);
                if (next < 0 || centers[next] < 0 || centers[next] == centers[i] || detached.Contains(next)) continue;
                borders.Add((Math.Min(centers[i], centers[next]), Math.Max(centers[i], centers[next])));
            }
        }
        var result = new TerritorialJurisdictions { CenterCaptureVersion = 2, CellCenters = centers,
            DetachedCells = detached.OrderBy(c => c).ToArray(), Borders = borders.OrderBy(b => b.Item1).ThenBy(b => b.Item2).Select(b => new JurisdictionBorder(b.Item1, b.Item2)).ToArray() };
        result.Validate(state);return result;
    }
}
