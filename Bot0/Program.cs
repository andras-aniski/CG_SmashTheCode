using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

// ── Constants ─────────────────────────────────────────────────────────────────
static class K
{
    public const int ROWS = 12, COLS = 6, CAPACITY = 72;
    public const int EMPTY = 9, SKULL = 0;
    public const int PAIRS = 8;
    public const int NUISANCE_DIV = 70;
    public const int SKULLS_PER_LINE = 6;
    public const int CHAIN_MIN = 4;
    public const int TIMEOUT_MS = 90;
    public const int OPP_TIMEOUT_MS = 22;
    public const int MAX_GEN = 7, OPP_MAX_GEN = 3;
    public const int BEAM_G0 = 22, BEAM_G1 = 484, BEAM = 110;
    public const int OPP_BEAM_G0 = 22, OPP_BEAM_G1 = 484, OPP_BEAM = 100;
    public const int WIN = 50_000;
    public const int MIN_FIRE = 4, MIN_OPP_FIRE = 3, MIN_COUNTER = 2;
    public const int SKULL_BONUS = 5;
}

// ── Always-on stderr logger ────────────────────────────────────────────────────
static class Log
{
    public static void Turn(int n) => CE($"\n=== TURN {n} ===");

    public static void Pairs(int[][] p)
    {
        var sb = new StringBuilder("PAIRS:");
        for (int i = 0; i < K.PAIRS; i++) sb.Append($" [{p[i][0]}{p[i][1]}]");
        CE(sb.ToString());
    }

    public static void Grids(int[,] my, int[,] opp, int ms, int os)
    {
        CE($"MY({ms})          OPP({os})");
        for (int r = 0; r < K.ROWS; r++)
        {
            var sb = new StringBuilder("  ");
            for (int c = 0; c < K.COLS; c++)
                sb.Append(my[r, c] == K.EMPTY ? '.' : (char)('0' + my[r, c]));
            sb.Append("    ");
            for (int c = 0; c < K.COLS; c++)
                sb.Append(opp[r, c] == K.EMPTY ? '.' : (char)('0' + opp[r, c]));
            CE(sb.ToString());
        }
    }

    public static void Gen(int gen, int pool, int best, long ms) =>
        CE($"  gen={gen} pool={pool} best={best} {ms}ms");

    public static void Move(int col, int rot, int fit, long ms, string tag) =>
        CE($"MOVE {col} {rot}  fit={fit}  {ms}ms  [{tag}]");

    public static void Info(string msg) => CE($"  {msg}");
    static void CE(string s) => Console.Error.WriteLine(s);
}

// ── Grid helpers ──────────────────────────────────────────────────────────────
static class GH
{
    static readonly (int dr, int dc)[] _4 = { (-1, 0), (1, 0), (0, -1), (0, 1) };

    public static int[,] Parse(string[] rows, out int spaces)
    {
        var g = new int[K.ROWS, K.COLS]; spaces = 0;
        for (int r = 0; r < K.ROWS; r++)
            for (int c = 0; c < K.COLS; c++)
            {
                char ch = rows[r][c];
                if (ch == '.') { g[r, c] = K.EMPTY; spaces++; }
                else g[r, c] = ch - '0';
            }
        return g;
    }

    // Row where a block lands when dropped in col. Returns -1 if column is full.
    public static int Land(int[,] g, int col)
    {
        for (int r = 0; r < K.ROWS; r++)
            if (g[r, col] != K.EMPTY) return r - 1;
        return K.ROWS - 1;
    }

    public static void Gravity(int[,] g)
    {
        for (int c = 0; c < K.COLS; c++)
        {
            int w = K.ROWS - 1;
            for (int r = K.ROWS - 1; r >= 0; r--)
                if (g[r, c] != K.EMPTY) g[w--, c] = g[r, c];
            while (w >= 0) g[w--, c] = K.EMPTY;
        }
    }

    public static int Spaces(int[,] g)
    {
        int n = 0;
        for (int r = 0; r < K.ROWS; r++)
            for (int c = 0; c < K.COLS; c++)
                if (g[r, c] == K.EMPTY) n++;
        return n;
    }

    public static IEnumerable<(int r, int c)> Nb(int row, int col)
    {
        foreach (var (dr, dc) in _4)
        {
            int nr = row + dr, nc = col + dc;
            if (nr >= 0 && nr < K.ROWS && nc >= 0 && nc < K.COLS)
                yield return (nr, nc);
        }
    }
}

// ── Puyo physics ──────────────────────────────────────────────────────────────
static class Phys
{
    // Drop pair (col, rot) onto grid. Mutates grid. Returns false on overflow.
    public static bool Drop(int[,] g, int[] pair, int col, int rot,
                            out int score, out int skulls)
    {
        score = skulls = 0;
        int v1 = pair[0], v2 = pair[1];
        int c1 = col, c2 = col;
        switch (rot)
        {
            case 0: c2 = col + 1; break;
            case 2: c2 = col - 1; break;
            case 3: int t = v1; v1 = v2; v2 = t; break; // vertical reversed
        }

        if (rot == 1 || rot == 3)
        {
            // Vertical: v1 drops first (bottom), v2 sits one row above.
            int r1 = GH.Land(g, c1);
            if (r1 < 0) return false;
            int r2 = r1 - 1;
            if (r2 < 0) return false;
            g[r1, c1] = v1;
            g[r2, c2] = v2;
        }
        else
        {
            // Horizontal: each block falls independently.
            int r1 = GH.Land(g, c1), r2 = GH.Land(g, c2);
            if (r1 < 0 || r2 < 0) return false;
            g[r1, c1] = v1;
            g[r2, c2] = v2;
        }

        (score, skulls) = Resolve(g);
        return true;
    }

    public static (int score, int skulls) Resolve(int[,] g)
    {
        int ts = 0, tk = 0, chain = 0;
        while (true)
        {
            var groups = ColorGroups(g);
            if (groups.Count == 0) break;

            // Capture B, colors, and per-group sizes before clearing (grid still intact).
            int B = 0;
            var colors = new HashSet<int>();
            var groupSizes = new int[groups.Count];
            for (int gi = 0; gi < groups.Count; gi++)
                foreach (var (r, c) in groups[gi]) { colors.Add(g[r, c]); B++; groupSizes[gi]++; }

            ts += TurnScore(B, colors.Count, groupSizes, chain);

            var rm = new HashSet<(int, int)>();
            foreach (var gr in groups) foreach (var cell in gr) rm.Add(cell);

            // Adjacent skulls are also cleared.
            foreach (var (r, c) in rm.ToArray())
                foreach (var (nr, nc) in GH.Nb(r, c))
                    if (g[nr, nc] == K.SKULL && rm.Add((nr, nc))) tk++;

            foreach (var (r, c) in rm) g[r, c] = K.EMPTY;
            GH.Gravity(g);
            chain++;
        }
        return (ts, tk);
    }

    static List<List<(int, int)>> ColorGroups(int[,] g)
    {
        var visited = new bool[K.ROWS, K.COLS];
        var result = new List<List<(int, int)>>();
        for (int r = 0; r < K.ROWS; r++)
            for (int c = 0; c < K.COLS; c++)
            {
                int v = g[r, c];
                if (v == K.EMPTY || v == K.SKULL || visited[r, c]) continue;
                var group = new List<(int, int)>();
                var stk = new Stack<(int, int)>();
                stk.Push((r, c)); visited[r, c] = true;
                while (stk.Count > 0)
                {
                    var (cr, cc) = stk.Pop(); group.Add((cr, cc));
                    foreach (var (nr, nc) in GH.Nb(cr, cc))
                        if (!visited[nr, nc] && g[nr, nc] == v)
                        { visited[nr, nc] = true; stk.Push((nr, nc)); }
                }
                if (group.Count >= K.CHAIN_MIN) result.Add(group);
            }
        return result;
    }

    // Score formula: (10 * B) * clamp(CP + CB + GB, 1, 999)
    // GB is summed per group: 0 for 4, 1 for 5, ..., 6 for 10, 8 for 11+
    static int TurnScore(int B, int nColors, int[] groupSizes, int chain)
    {
        int CP = 0;
        if (chain >= 1) { CP = 8; for (int i = 1; i < chain; i++) CP *= 2; }
        int CB = nColors <= 1 ? 0 : (int)Math.Pow(2, nColors - 1); // 0/2/4/8/16
        int GB = 0;
        foreach (int sz in groupSizes)
            GB += sz <= 4 ? 0 : sz <= 10 ? sz - 4 : 8;
        return 10 * B * Math.Max(1, Math.Min(999, CP + CB + GB));
    }

    // Count small color groups adjacent to empty cells: result[0]=2-conn, result[1]=3-conn
    public static int[] NearChains(int[,] g)
    {
        var visited = new bool[K.ROWS, K.COLS];
        var counts = new int[2];
        for (int r = 0; r < K.ROWS; r++)
            for (int c = 0; c < K.COLS; c++)
            {
                int v = g[r, c];
                if (v == K.EMPTY || v == K.SKULL || visited[r, c]) continue;
                var stk = new Stack<(int, int)>(); stk.Push((r, c));
                visited[r, c] = true; int sz = 0;
                bool hasEmptyNb = false;
                while (stk.Count > 0)
                {
                    var (cr, cc) = stk.Pop(); sz++;
                    foreach (var (nr, nc) in GH.Nb(cr, cc))
                    {
                        if (g[nr, nc] == K.EMPTY) hasEmptyNb = true;
                        else if (!visited[nr, nc] && g[nr, nc] == v)
                        { visited[nr, nc] = true; stk.Push((nr, nc)); }
                    }
                }
                if (!hasEmptyNb) continue; // buried group — can't grow
                if (sz == 2) counts[0]++;
                else if (sz == 3) counts[1]++;
            }
        return counts;
    }
}

// ── Search node ───────────────────────────────────────────────────────────────
class Node
{
    public int[][] Pairs;                      // shared, read-only
    public int[,] MyGrid, OppGrid;
    public int MyScore, OppScore;
    public double MyNuis, OppNuis;
    public int MySpaces, OppSpaces;
    public int MyTotalScore;
    public int MyTotalSkulls, OppTotalSkulls;
    public int[] MySkullLines, OppSkullLines;  // skull lines sent per depth
    public int[] MyTurnScores, OppTurnScores;  // raw score per depth
    public int[] MyNearChains, OppNearChains;  // [0]=2-conn [1]=3-conn
    public int Depth, GameResult;
    public int MyFit, OppFit;
    public int[]? FirstMove;                   // [col, rot] of depth-0 move
    public int InitMySpaces, InitMyScore, InitOppScore;

    public Node(int[][] pairs, int[,] myGrid, int[,] oppGrid,
                int myScore, int oppScore, int mySpaces, int oppSpaces)
    {
        Pairs = pairs;
        MyGrid = (int[,])myGrid.Clone(); OppGrid = (int[,])oppGrid.Clone();
        MyScore = InitMyScore = myScore;
        OppScore = InitOppScore = oppScore;
        MySpaces = InitMySpaces = mySpaces; OppSpaces = oppSpaces;
        MySkullLines = new int[K.PAIRS]; OppSkullLines = new int[K.PAIRS];
        MyTurnScores = new int[K.PAIRS]; OppTurnScores = new int[K.PAIRS];
        MyNearChains = new int[2]; OppNearChains = new int[2];
    }

    public Node(Node s)
    {
        Pairs = s.Pairs;
        MyGrid = (int[,])s.MyGrid.Clone(); OppGrid = (int[,])s.OppGrid.Clone();
        MyScore = s.MyScore; InitMyScore = s.InitMyScore;
        OppScore = s.OppScore; InitOppScore = s.InitOppScore;
        MyNuis = s.MyNuis; OppNuis = s.OppNuis;
        MySpaces = s.MySpaces; InitMySpaces = s.InitMySpaces; OppSpaces = s.OppSpaces;
        MyTotalScore = s.MyTotalScore;
        MyTotalSkulls = s.MyTotalSkulls; OppTotalSkulls = s.OppTotalSkulls;
        MySkullLines = (int[])s.MySkullLines.Clone();
        OppSkullLines = (int[])s.OppSkullLines.Clone();
        MyTurnScores = (int[])s.MyTurnScores.Clone();
        OppTurnScores = (int[])s.OppTurnScores.Clone();
        MyNearChains = (int[])s.MyNearChains.Clone();
        OppNearChains = (int[])s.OppNearChains.Clone();
        Depth = s.Depth; GameResult = s.GameResult;
        MyFit = s.MyFit; OppFit = s.OppFit;
        FirstMove = s.FirstMove; // immutable once set
    }

    // Play current pair for faction (1=player, -1=opponent). Always advances Depth.
    public bool Play(int col, int rot, int faction)
    {
        var grid = faction == 1 ? MyGrid : OppGrid;
        if (!Phys.Drop(grid, Pairs[Depth], col, rot, out int score, out int skulls))
        {
            GameResult = -faction; // overflow → that faction loses
            Depth++;
            return false;
        }
        if (faction == 1)
        {
            if (Depth == 0) FirstMove = new[] { col, rot };
            MyTurnScores[Depth] = score;
            MyTotalScore += score; MyScore += score;
            MyNuis += (double)score / K.NUISANCE_DIV;
            MySkullLines[Depth] = (int)(MyNuis / K.SKULLS_PER_LINE);
            if (MySkullLines[Depth] > 0) MyNuis %= K.SKULLS_PER_LINE;
            MyTotalSkulls += skulls;
            MySpaces = GH.Spaces(MyGrid);
            MyNearChains = Phys.NearChains(MyGrid);
        }
        else
        {
            OppTurnScores[Depth] = score;
            OppScore += score;
            OppNuis += (double)score / K.NUISANCE_DIV;
            OppSkullLines[Depth] = (int)(OppNuis / K.SKULLS_PER_LINE);
            if (OppSkullLines[Depth] > 0) OppNuis %= K.SKULLS_PER_LINE;
            OppTotalSkulls += skulls;
            OppSpaces = GH.Spaces(OppGrid);
            OppNearChains = Phys.NearChains(OppGrid);
        }
        Depth++;
        return true;
    }
}

// ── Opponent beam search (DummyBot) ───────────────────────────────────────────
static class OppSim
{
    public static void Run(Node init)
    {
        var sw = Game.SW;
        Node[] pool = new[] { init };
        Node? best = null;

        for (int gen = 0; gen <= K.OPP_MAX_GEN; gen++)
        {
            if (sw.ElapsedMilliseconds > K.OPP_TIMEOUT_MS) break;
            int beam = gen == 0 ? K.OPP_BEAM_G0 : gen == 1 ? K.OPP_BEAM_G1 : K.OPP_BEAM;
            var next = new Node[beam];
            int ni = 0, minIdx = -1, minFit = int.MaxValue;

            foreach (var node in pool)
            {
                if (sw.ElapsedMilliseconds > K.OPP_TIMEOUT_MS) break;
                for (int rot = 0; rot < 4; rot++)
                {
                    if (sw.ElapsedMilliseconds > K.OPP_TIMEOUT_MS) break;
                    for (int col = 0; col < K.COLS; col++)
                    {
                        if (!Valid(col, rot)) continue;
                        var p = node.Pairs[node.Depth];
                        if (p[0] == p[1] && rot > 1) continue;

                        var w = new Node(node);
                        w.Play(col, rot, -1);
                        if (w.GameResult != 0) continue; // opp overflowed

                        OppEval(w);

                        if (ni < beam)
                        {
                            next[ni++] = w; minIdx = -1; minFit = int.MaxValue;
                        }
                        else
                        {
                            if (minIdx == -1) FindMin(next, ni, out minIdx, out minFit);
                            if (w.OppFit > minFit)
                            { next[minIdx] = w; minIdx = -1; minFit = int.MaxValue; }
                        }
                    }
                }
            }

            if (ni == 0) break;
            pool = next[..ni];
            best = pool.MaxBy(n => n.OppFit);
            if (best != null && best.OppSkullLines.Sum() >= K.MIN_OPP_FIRE) break;
        }

        if (best != null)
        {
            init.OppFit = best.OppFit;
            init.OppTurnScores = best.OppTurnScores;
            init.OppSkullLines = best.OppSkullLines;
            init.OppTotalSkulls = best.OppTotalSkulls;
            init.OppNearChains = best.OppNearChains;
        }
    }

    static void OppEval(Node n)
    {
        n.OppFit = n.OppScore - n.InitOppScore;
        if (n.GameResult == -1) n.OppFit += K.WIN;  // player overflowed → opp wins
        else if (n.GameResult == 1) n.OppFit -= K.WIN; // opp overflowed → opp loses
        for (int i = 0; i < 2; i++) n.OppFit += (i + 2) * (i + 2) * n.OppNearChains[i];
        n.OppFit += n.OppTotalSkulls * K.SKULL_BONUS;
    }

    static void FindMin(Node[] arr, int len, out int idx, out int fit)
    {
        idx = -1; fit = int.MaxValue;
        for (int i = 0; i < len; i++)
            if (arr[i] != null && arr[i].OppFit < fit) { fit = arr[i].OppFit; idx = i; }
    }

    static bool Valid(int col, int rot) =>
        col >= 0 && col < K.COLS &&
        !(rot == 0 && col >= K.COLS - 1) &&
        !(rot == 2 && col == 0);
}

// ── Player evaluator ──────────────────────────────────────────────────────────
static class Eval
{
    public static void Run(Node n)
    {
        // Discount deeper total scores (prefer sooner chains).
        n.MyFit = (int)(n.MyTotalScore * Math.Pow(0.7, Math.Max(0, n.Depth - 1)));
        if (n.GameResult == 1) n.MyFit += K.WIN;
        else if (n.GameResult == -1) n.MyFit -= K.WIN;

        // Near-chain groups (weighted by group size).
        for (int i = 0; i < 2; i++) n.MyFit += (i + 2) * (i + 2) * n.MyNearChains[i];
        n.MyFit += n.MyTotalSkulls * K.SKULL_BONUS;

        // Small center-column bias.
        if (n.FirstMove != null && (n.FirstMove[0] == 2 || n.FirstMove[0] == 3))
            n.MyFit += 5;

        // Space management: reward opening the board if it was crowded.
        if (n.InitMySpaces < 14 && n.MySpaces > 14 + n.Depth * 2)
            n.MyFit += n.MySpaces * 5;
        else
            n.MyFit -= K.CAPACITY - n.MySpaces; // penalise dense grids

        // Skull lines sent are good.
        n.MyFit += n.MySkullLines.Sum() * 50;

        // Subtract opponent's threat.
        n.MyFit -= n.OppFit;
    }
}

// ── Main game loop & player beam search ───────────────────────────────────────
static class Game
{
    public static readonly Stopwatch SW = new Stopwatch();

    public static string BestMove(Node init)
    {
        SW.Restart();
        OppSim.Run(init);
        Log.Info($"opp: fit={init.OppFit} skulls=[{string.Join(",", init.OppSkullLines.Take(K.OPP_MAX_GEN + 1))}] ms={SW.ElapsedMilliseconds}");

        Node[] pool = new[] { init };
        Node? bestEver = null, bestPass = null, bestFirst = null;

        for (int gen = 0; gen <= K.MAX_GEN && SW.ElapsedMilliseconds < K.TIMEOUT_MS; gen++)
        {
            int beam = gen == 0 ? K.BEAM_G0 : gen == 1 ? K.BEAM_G1 : K.BEAM;
            var next = new Node[beam];
            int ni = 0, minIdx = -1, minFit = int.MaxValue;

            foreach (var node in pool)
            {
                if (SW.ElapsedMilliseconds >= K.TIMEOUT_MS) break;
                for (int rot = 0; rot < 4; rot++)
                {
                    if (SW.ElapsedMilliseconds >= K.TIMEOUT_MS) break;
                    for (int col = 0; col < K.COLS; col++)
                    {
                        if (!Valid(col, rot)) continue;
                        var p = node.Pairs[node.Depth];
                        if (p[0] == p[1] && rot > 1) continue;

                        var w = new Node(node);
                        w.Play(col, rot, 1);
                        if (w.GameResult == -1) continue; // we overflowed
                        if (w.GameResult == 1) return Fmt(w, "WIN");

                        Eval.Run(w);

                        if (gen == 0 && (bestFirst == null || w.MySkullLines[0] > bestFirst.MySkullLines[0]))
                            bestFirst = w;
                        if (w.MyTurnScores[0] < 40 && (bestPass == null || w.MyFit > bestPass.MyFit))
                            bestPass = w;

                        if (ni < beam)
                        {
                            next[ni++] = w; minIdx = -1; minFit = int.MaxValue;
                        }
                        else
                        {
                            if (minIdx == -1) FindMin(next, ni, out minIdx, out minFit);
                            if (w.MyFit > minFit)
                            { next[minIdx] = w; minIdx = -1; minFit = int.MaxValue; }
                        }
                    }
                }
            }

            if (ni == 0) return bestEver != null ? Fmt(bestEver, "NO_NEXT") : "0 0";
            pool = next[..ni];
            bestEver = pool.MaxBy(n => n.MyFit);

            // Aggressive fire: enough skull lines generated at this depth.
            if (bestEver != null && bestEver.MySkullLines[gen] >= K.MIN_FIRE)
            {
                Log.Gen(gen, ni, bestEver.MyFit, SW.ElapsedMilliseconds);
                return Fmt(bestEver, $"FIRE_G{gen}");
            }

            // Defensive counter: opponent is about to send a large attack.
            if (gen <= K.OPP_MAX_GEN && init.OppSkullLines.Sum() >= K.MIN_OPP_FIRE && bestFirst != null)
            {
                int requiredLines = init.OppSkullLines.Sum() > K.MIN_OPP_FIRE ? 1 : K.MIN_COUNTER;
                if (bestFirst.MySkullLines[gen] >= requiredLines)
                {
                    Log.Gen(gen, ni, bestFirst.MyFit, SW.ElapsedMilliseconds);
                    return Fmt(bestFirst, $"COUNTER_G{gen}");
                }
            }

            Log.Gen(gen, ni, bestEver?.MyFit ?? 0, SW.ElapsedMilliseconds);
        }

        // Final selection: prefer first-turn skull fire, then safe build moves.
        Array.Sort(pool, (a, b) => (b?.MyFit ?? int.MinValue) - (a?.MyFit ?? int.MinValue));
        foreach (var s in pool)
        {
            if (s == null) continue;
            if (s.MySkullLines[0] >= K.MIN_FIRE) return Fmt(s, "SKULL_1ST");
            if (s.MyTurnScores[0] < 40) return Fmt(s, "BUILD");
        }
        if (bestPass != null) return Fmt(bestPass, "PASS");
        return pool[0] != null ? Fmt(pool[0], "DEFAULT") : "0 0";
    }

    static string Fmt(Node n, string tag)
    {
        Log.Move(n.FirstMove![0], n.FirstMove[1], n.MyFit, SW.ElapsedMilliseconds, tag);
        return $"{n.FirstMove[0]} {n.FirstMove[1]}";
    }

    static void FindMin(Node[] arr, int len, out int idx, out int fit)
    {
        idx = -1; fit = int.MaxValue;
        for (int i = 0; i < len; i++)
            if (arr[i] != null && arr[i].MyFit < fit) { fit = arr[i].MyFit; idx = i; }
    }

    static bool Valid(int col, int rot) =>
        col >= 0 && col < K.COLS &&
        !(rot == 0 && col >= K.COLS - 1) &&
        !(rot == 2 && col == 0);
}

// ── Entry point ───────────────────────────────────────────────────────────────
class Player
{
    static void Main(string[] _)
    {
        int turn = 0;
        var myRows = new string[K.ROWS];
        var oppRows = new string[K.ROWS];

        while (true)
        {
            turn++; Log.Turn(turn);

            var pairs = new int[K.PAIRS][];
            for (int i = 0; i < K.PAIRS; i++)
            {
                var parts = Console.ReadLine()!.Split(' ');
                int a = int.TryParse(parts[0], out int va) ? va : K.EMPTY;
                int b = int.TryParse(parts[1], out int vb) ? vb : K.EMPTY;
                pairs[i] = new[] { a, b };
            }
            Log.Pairs(pairs);

            int myScore = int.Parse(Console.ReadLine()!);
            for (int i = 0; i < K.ROWS; i++) myRows[i] = Console.ReadLine()!;
            int oppScore = int.Parse(Console.ReadLine()!);
            for (int i = 0; i < K.ROWS; i++) oppRows[i] = Console.ReadLine()!;

            var myGrid = GH.Parse(myRows, out int mySpaces);
            var oppGrid = GH.Parse(oppRows, out int oppSpaces);
            Log.Grids(myGrid, oppGrid, myScore, oppScore);

            var node = new Node(pairs, myGrid, oppGrid, myScore, oppScore, mySpaces, oppSpaces);
            Console.WriteLine(Game.BestMove(node));
        }
    }
}
