using System.Diagnostics;
using System.Text;

var engine = new SmashEngine(args);
return engine.Run();

// ══════════════════════════════════════════════════════════════════════════════
class SmashEngine
{
    const int ROWS = 12, COLS = 6;
    const int EMPTY = 9, SKULL = 0;
    const int MAX_TURNS = 200;
    const double NUISANCE_DIV = 70.0;
    const int SKULLS_PER_LINE = 6;
    const int CHAIN_MIN = 4;
    const int PAIRS_AHEAD = 8;

    readonly string[] _botNames = { "Bot0", "Bot1" };
    string? _bot0Cmd, _bot1Cmd;
    int _seed = -1, _games = 1, _timeoutMs = 5000;
    bool _verbose;

    public SmashEngine(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--bot0": _bot0Cmd = args[++i]; break;
                case "--bot1": _bot1Cmd = args[++i]; break;
                case "--seed": _seed = int.Parse(args[++i]); break;
                case "--games": _games = int.Parse(args[++i]); break;
                case "--timeout": _timeoutMs = int.Parse(args[++i]); break;
                case "--verbose" or "-v": _verbose = true; break;
                case "--help" or "-h": PrintHelp(); Environment.Exit(0); break;
            }
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            Smash The Code Engine — Local Game Runner

            Usage: dotnet run --project Engine -- [options]

            Options:
              --bot0 <cmd>      Command to run Bot0 (default: auto-build Bot0)
              --bot1 <cmd>      Command to run Bot1 (default: auto-build Bot1)
              --seed <n>        Random seed (default: random)
              --games <n>       Number of games to play (default: 1)
              --timeout <ms>    Per-turn timeout in ms (default: 5000)
              --verbose, -v     Show turn details and bot stderr
              --help, -h        Show this help
            """);
    }

    // ── Main entry ──────────────────────────────────────────────────────────
    public int Run()
    {
        string solRoot = FindSolutionRoot();
        string? bot0 = _bot0Cmd ?? AutoBuild(solRoot, "Bot0");
        string? bot1 = _bot1Cmd ?? AutoBuild(solRoot, "Bot1");
        if (bot0 == null || bot1 == null) return 1;

        int w0 = 0, w1 = 0, dr = 0;
        for (int g = 0; g < _games; g++)
        {
            int seed = _seed >= 0 ? _seed + g : Random.Shared.Next();
            int result = RunGame(bot0, bot1, seed);
            if (result == 0) w0++; else if (result == 1) w1++; else dr++;
            Console.WriteLine($"Game {g + 1}/{_games}: seed={seed} -> {Res(result)}  " +
                              $"[Bot0:{w0} Bot1:{w1} Draw:{dr}]");
        }

        if (_games > 1)
        {
            Console.WriteLine($"\n=== RESULTS ({_games} games) ===");
            Console.WriteLine($"Bot0: {w0} wins ({100.0 * w0 / _games:F1}%)");
            Console.WriteLine($"Bot1: {w1} wins ({100.0 * w1 / _games:F1}%)");
            if (dr > 0) Console.WriteLine($"Draws: {dr}");
        }
        return 0;
    }

    static string Res(int r) => r switch { 0 => "Bot0 wins", 1 => "Bot1 wins", _ => "Draw" };

    // ── Single game execution ───────────────────────────────────────────────
    // Returns: 0 = Bot0 wins, 1 = Bot1 wins, -1 = draw
    int RunGame(string bot0Cmd, string bot1Cmd, int seed)
    {
        var rng = new Random(seed);

        // Pre-generate all pairs
        var pairs = new int[MAX_TURNS + PAIRS_AHEAD][];
        for (int i = 0; i < pairs.Length; i++)
            pairs[i] = new[] { rng.Next(1, 6), rng.Next(1, 6) };

        // Init empty grids
        var grids = new int[2][,];
        for (int p = 0; p < 2; p++)
        {
            grids[p] = new int[ROWS, COLS];
            for (int r = 0; r < ROWS; r++)
                for (int c = 0; c < COLS; c++)
                    grids[p][r, c] = EMPTY;
        }

        var scores = new int[2];
        var nuisance = new double[2];
        var pendingSkulls = new int[2];

        Process? proc0 = null, proc1 = null;
        try
        {
            proc0 = StartBot(bot0Cmd, _botNames[0]);
            proc1 = StartBot(bot1Cmd, _botNames[1]);
            var procs = new[] { proc0, proc1 };

            for (int turn = 1; turn <= MAX_TURNS; turn++)
            {
                // 1. Drop pending skull lines onto each player's grid
                for (int p = 0; p < 2; p++)
                {
                    if (pendingSkulls[p] > 0)
                    {
                        DropSkullLines(grids[p], pendingSkulls[p]);
                        pendingSkulls[p] = 0;
                    }
                }

                if (_verbose) PrintTurn(turn, grids, scores, pairs[turn - 1]);

                // 2. Send game state to both bots
                for (int p = 0; p < 2; p++)
                {
                    string state = BuildState(pairs, turn, grids[p], grids[1 - p],
                                              scores[p], scores[1 - p]);
                    procs[p]!.StandardInput.Write(state);
                    procs[p]!.StandardInput.Flush();
                }

                // 3. Read moves from both bots
                string? move0 = ReadLineTimeout(proc0!.StandardOutput, _timeoutMs);
                string? move1 = ReadLineTimeout(proc1!.StandardOutput, _timeoutMs);

                // 4. Parse and validate moves
                bool v0 = ParseMove(move0, out int col0, out int rot0) && IsValidMove(col0, rot0);
                bool v1 = ParseMove(move1, out int col1, out int rot1) && IsValidMove(col1, rot1);

                if (_verbose)
                    Console.WriteLine($"  Bot0: {move0?.Trim() ?? "TIMEOUT"}  |  " +
                                      $"Bot1: {move1?.Trim() ?? "TIMEOUT"}");

                if (!v0 && !v1) return End(turn, -1, "Both bots gave invalid output");
                if (!v0) return End(turn, 1, $"Bot0 invalid output: '{move0?.Trim()}'");
                if (!v1) return End(turn, 0, $"Bot1 invalid output: '{move1?.Trim()}'");

                // 5. Apply moves to respective grids
                bool ok0 = DropPair(grids[0], pairs[turn - 1], col0, rot0, out int ts0);
                bool ok1 = DropPair(grids[1], pairs[turn - 1], col1, rot1, out int ts1);

                if (!ok0 && !ok1) return End(turn, -1, "Both bots overflowed");
                if (!ok0) return End(turn, 1, "Bot0 grid overflow");
                if (!ok1) return End(turn, 0, "Bot1 grid overflow");

                scores[0] += ts0;
                scores[1] += ts1;

                // 6. Accumulate nuisance → skull lines for opponent
                for (int p = 0; p < 2; p++)
                {
                    int ts = p == 0 ? ts0 : ts1;
                    nuisance[p] += ts / NUISANCE_DIV;
                    int lines = (int)(nuisance[p] / SKULLS_PER_LINE);
                    if (lines > 0)
                    {
                        nuisance[p] -= lines * SKULLS_PER_LINE;
                        pendingSkulls[1 - p] += lines;
                    }
                }

                if (_verbose && (ts0 > 0 || ts1 > 0))
                    Console.WriteLine($"  Score: +{ts0}/+{ts1}  Total: {scores[0]}/{scores[1]}  " +
                                      $"Skulls pending: Bot0←{pendingSkulls[0]} Bot1←{pendingSkulls[1]}");
            }

            // Time limit — higher score wins
            if (scores[0] > scores[1]) return End(MAX_TURNS, 0, $"Time limit {scores[0]} vs {scores[1]}");
            if (scores[1] > scores[0]) return End(MAX_TURNS, 1, $"Time limit {scores[1]} vs {scores[0]}");
            return End(MAX_TURNS, -1, $"Time limit draw at {scores[0]}");
        }
        finally
        {
            KillProc(proc0);
            KillProc(proc1);
        }
    }

    int End(int turn, int winner, string reason)
    {
        Console.WriteLine($"  Turn {turn}: {reason} -> {Res(winner)}");
        return winner;
    }

    // ── Bot process management ──────────────────────────────────────────────
    Process StartBot(string cmd, string name)
    {
        string fileName, arguments;
        int sp = cmd.IndexOf(' ');
        if (sp > 0) { fileName = cmd[..sp]; arguments = cmd[(sp + 1)..]; }
        else { fileName = cmd; arguments = ""; }

        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null && _verbose)
                Console.Error.WriteLine($"[{name}] {e.Data}");
        };
        proc.Start();
        proc.BeginErrorReadLine();
        return proc;
    }

    static void KillProc(Process? p)
    {
        if (p == null) return;
        try { if (!p.HasExited) p.Kill(true); } catch { }
        try { p.Dispose(); } catch { }
    }

    static string? ReadLineTimeout(StreamReader reader, int timeoutMs)
    {
        var task = Task.Run(() => reader.ReadLine());
        return task.Wait(timeoutMs) ? task.Result : null;
    }

    // ── State serialization (CodingGame protocol) ───────────────────────────
    static string BuildState(int[][] pairs, int turn, int[,] myGrid, int[,] oppGrid,
                             int myScore, int oppScore)
    {
        var sb = new StringBuilder(256);
        for (int i = 0; i < PAIRS_AHEAD; i++)
            sb.Append(pairs[turn - 1 + i][0]).Append(' ')
              .Append(pairs[turn - 1 + i][1]).Append('\n');

        sb.Append(myScore).Append('\n');
        AppendGrid(sb, myGrid);
        sb.Append(oppScore).Append('\n');
        AppendGrid(sb, oppGrid);
        return sb.ToString();
    }

    static void AppendGrid(StringBuilder sb, int[,] grid)
    {
        for (int r = 0; r < ROWS; r++)
        {
            for (int c = 0; c < COLS; c++)
                sb.Append(grid[r, c] == EMPTY ? '.' : (char)('0' + grid[r, c]));
            sb.Append('\n');
        }
    }

    // ── Move parsing ────────────────────────────────────────────────────────
    static bool ParseMove(string? line, out int col, out int rot)
    {
        col = rot = 0;
        if (string.IsNullOrWhiteSpace(line)) return false;
        var parts = line.Trim().Split(' ');
        return parts.Length >= 2 &&
               int.TryParse(parts[0], out col) &&
               int.TryParse(parts[1], out rot);
    }

    static bool IsValidMove(int col, int rot)
    {
        if (col < 0 || col >= COLS || rot < 0 || rot > 3) return false;
        if (rot == 0 && col >= COLS - 1) return false;
        if (rot == 2 && col == 0) return false;
        return true;
    }

    // ── Game physics ────────────────────────────────────────────────────────
    static int Land(int[,] g, int col)
    {
        for (int r = 0; r < ROWS; r++)
            if (g[r, col] != EMPTY) return r - 1;
        return ROWS - 1;
    }

    static void Gravity(int[,] g)
    {
        for (int c = 0; c < COLS; c++)
        {
            int w = ROWS - 1;
            for (int r = ROWS - 1; r >= 0; r--)
                if (g[r, c] != EMPTY) g[w--, c] = g[r, c];
            while (w >= 0) g[w--, c] = EMPTY;
        }
    }

    static bool DropPair(int[,] g, int[] pair, int col, int rot, out int score)
    {
        score = 0;
        int v1 = pair[0], v2 = pair[1];
        int c1 = col, c2 = col;

        switch (rot)
        {
            case 0: c2 = col + 1; break;
            case 2: c2 = col - 1; break;
            case 3: (v1, v2) = (v2, v1); break;
        }

        if (rot == 1 || rot == 3)
        {
            int r1 = Land(g, c1);
            if (r1 < 0) return false;
            int r2 = r1 - 1;
            if (r2 < 0) return false;
            g[r1, c1] = v1;
            g[r2, c1] = v2;
        }
        else
        {
            int r1 = Land(g, c1), r2 = Land(g, c2);
            if (r1 < 0 || r2 < 0) return false;
            g[r1, c1] = v1;
            g[r2, c2] = v2;
        }

        score = Resolve(g);
        return true;
    }

    static int Resolve(int[,] g)
    {
        int totalScore = 0, chain = 0;
        while (true)
        {
            var groups = FindColorGroups(g);
            if (groups.Count == 0) break;

            int B = 0;
            var colors = new HashSet<int>();
            var groupSizes = new List<int>();

            foreach (var group in groups)
            {
                int sz = 0;
                foreach (var (r, c) in group)
                {
                    colors.Add(g[r, c]);
                    sz++;
                }
                B += sz;
                groupSizes.Add(sz);
            }

            totalScore += CalcStepScore(B, colors.Count, groupSizes, chain);

            // Collect cells to clear (color groups + adjacent skulls)
            var toClear = new HashSet<(int r, int c)>();
            foreach (var group in groups)
                foreach (var cell in group)
                    toClear.Add(cell);

            foreach (var (r, c) in toClear.ToArray())
                foreach (var (nr, nc) in Neighbors(r, c))
                    if (g[nr, nc] == SKULL)
                        toClear.Add((nr, nc));

            foreach (var (r, c) in toClear)
                g[r, c] = EMPTY;

            Gravity(g);
            chain++;
        }
        return totalScore;
    }

    static List<List<(int r, int c)>> FindColorGroups(int[,] g)
    {
        var visited = new bool[ROWS, COLS];
        var result = new List<List<(int r, int c)>>();

        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
            {
                int v = g[r, c];
                if (v == EMPTY || v == SKULL || visited[r, c]) continue;

                var group = new List<(int, int)>();
                var stk = new Stack<(int, int)>();
                stk.Push((r, c));
                visited[r, c] = true;

                while (stk.Count > 0)
                {
                    var (cr, cc) = stk.Pop();
                    group.Add((cr, cc));
                    foreach (var (nr, nc) in Neighbors(cr, cc))
                        if (!visited[nr, nc] && g[nr, nc] == v)
                        {
                            visited[nr, nc] = true;
                            stk.Push((nr, nc));
                        }
                }

                if (group.Count >= CHAIN_MIN)
                    result.Add(group);
            }

        return result;
    }

    // score = (10 * B) * clamp(CP + CB + GB, 1, 999)
    static int CalcStepScore(int B, int nColors, List<int> groupSizes, int chain)
    {
        // Chain power: 0 for first step, 8 for second, doubles thereafter
        int CP = 0;
        if (chain >= 1) { CP = 8; for (int i = 1; i < chain; i++) CP *= 2; }

        // Color bonus
        int CB = nColors switch { <= 1 => 0, 2 => 2, 3 => 4, 4 => 8, _ => 16 };

        // Group bonus: per-group based on size, then summed
        int GB = 0;
        foreach (int sz in groupSizes)
            GB += sz <= 4 ? 0 : sz <= 10 ? sz - 4 : 8;

        int mult = Math.Max(1, Math.Min(999, CP + CB + GB));
        return 10 * B * mult;
    }

    static void DropSkullLines(int[,] g, int lines)
    {
        for (int line = 0; line < lines; line++)
            for (int c = 0; c < COLS; c++)
            {
                int r = Land(g, c);
                if (r >= 0) g[r, c] = SKULL;
            }
    }

    static IEnumerable<(int r, int c)> Neighbors(int row, int col)
    {
        if (row > 0) yield return (row - 1, col);
        if (row < ROWS - 1) yield return (row + 1, col);
        if (col > 0) yield return (row, col - 1);
        if (col < COLS - 1) yield return (row, col + 1);
    }

    // ── Display helpers ─────────────────────────────────────────────────────
    void PrintTurn(int turn, int[][,] grids, int[] scores, int[] pair)
    {
        Console.WriteLine($"\n--- Turn {turn} --- Pair: [{pair[0]},{pair[1]}]  " +
                          $"Score: {scores[0]} vs {scores[1]}");
        for (int r = 0; r < ROWS; r++)
        {
            var sb = new StringBuilder("  ");
            for (int c = 0; c < COLS; c++)
                sb.Append(grids[0][r, c] == EMPTY ? '.' : (char)('0' + grids[0][r, c]));
            sb.Append("  |  ");
            for (int c = 0; c < COLS; c++)
                sb.Append(grids[1][r, c] == EMPTY ? '.' : (char)('0' + grids[1][r, c]));
            Console.WriteLine(sb);
        }
    }

    // ── Build helpers ───────────────────────────────────────────────────────
    static string FindSolutionRoot()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            dir = Path.GetDirectoryName(dir);
            if (dir != null &&
                (Directory.GetFiles(dir, "*.slnx").Length > 0 ||
                 Directory.GetFiles(dir, "*.sln").Length > 0))
                return dir;
        }
        return Directory.GetCurrentDirectory();
    }

    static string? AutoBuild(string solRoot, string botName)
    {
        string projPath = Path.Combine(solRoot, botName, $"{botName}.csproj");
        if (!File.Exists(projPath))
        {
            Console.Error.WriteLine($"ERROR: {projPath} not found");
            return null;
        }

        Console.Error.Write($"Building {botName} (Release)... ");
        var psi = new ProcessStartInfo("dotnet", $"build \"{projPath}\" -c Release --nologo -v q")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        var p = Process.Start(psi)!;
        // Read both streams to avoid deadlock
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        p.WaitForExit(60000);
        string stderr = stderrTask.Result;
        if (p.ExitCode != 0)
        {
            Console.Error.WriteLine("FAILED");
            Console.Error.WriteLine(stderr);
            return null;
        }
        Console.Error.WriteLine("OK");

        // Find the built executable
        string binDir = Path.Combine(solRoot, botName, "bin", "Release");
        if (Directory.Exists(binDir))
        {
            foreach (string tfmDir in Directory.GetDirectories(binDir))
            {
                string exe = Path.Combine(tfmDir, botName + ".exe");
                if (File.Exists(exe)) return exe;
                exe = Path.Combine(tfmDir, botName);
                if (File.Exists(exe)) return exe;
            }
        }

        // Fallback to dotnet run
        return $"dotnet run --project \"{projPath}\" -c Release --no-build";
    }
}
