using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

class Player
{
    static void Main(string[] args)
    {
        var game = new Game();
        int[,] myGrid;
        int[,] oppGrid;

        int mySpaces = 0;
        int oppSpaces = 0;

        bool _hasEmptyCell = false;
        int _maxToFillWithSkulls = 0;

        int turn = 0;
        int[][] pairs = new int[Constants.ADVANCE_TURNS][];
        Sim sim;

        while (true)
        {
            turn++;

            myGrid = new int[Constants.GRID_ROWS, Constants.GRID_COLUMNS];
            oppGrid = new int[Constants.GRID_ROWS, Constants.GRID_COLUMNS];

            mySpaces = 0;
            oppSpaces = 0;

            for (int i = 0; i < 8; i++)
            {
                string[] inputs = Console.ReadLine().Split(' ');
                int value1;
                int value2;
                if (!int.TryParse(inputs[0], out value1))
                    value1 = Constants.CELL_EMPTY;
                if (!int.TryParse(inputs[1], out value2))
                    value2 = Constants.CELL_EMPTY;

                pairs[i] = new[] { value1, value2 };
                //Logger.Log($"\"{string.Join(" ", inputs)}\",");
            }
            int score1 = int.Parse(Console.ReadLine());
            for (int i = 0; i < 12; i++)
            {
                var row = Console.ReadLine(); // One line of the map ('.' = empty, '0' = skull block, '1' to '5' = colored block)
                for (var j = 0; j < Constants.GRID_COLUMNS; j++)
                {
                    int value;
                    if (int.TryParse(row[j].ToString(), out value))
                        myGrid[i, j] = value;
                    else
                    {
                        myGrid[i, j] = Constants.CELL_EMPTY;
                        mySpaces++;
                    }
                }
            }

            Constants.GetInput(myGrid);

            int score2 = int.Parse(Console.ReadLine());

            for (int i = 0; i < 12; i++)
            {
                _hasEmptyCell = false;
                var row = Console.ReadLine();
                for (var j = 0; j < Constants.GRID_COLUMNS; j++)
                {
                    int value;
                    if (int.TryParse(row[j].ToString(), out value))
                        oppGrid[i, j] = value;
                    else
                    {
                        oppGrid[i, j] = Constants.CELL_EMPTY;
                        oppSpaces++;
                        _hasEmptyCell = true;
                    }
                }

                if (!_hasEmptyCell)
                    _maxToFillWithSkulls = i;
            }

            Game.SWatch.Restart();

            Constants.GetInput(oppGrid);

            sim = new Sim(turn, pairs, myGrid, oppGrid, score1, score2, mySpaces, oppSpaces);
            sim.OppMaxToFillWithSkulls = _maxToFillWithSkulls;
            Console.WriteLine(game.GetBestMove(sim));
        }
    }
}
public class Constants
{
    public const int GRID_ROWS = 12;
    public const int GRID_COLUMNS = 6;
    public const int MAX_SPACE = 72;

    public static int[] COLORS = { 1, 2, 3, 4, 5 };
    public const int ADVANCE_TURNS = 8;
    public const int BLOCKS_TO_SCORE = 4;
    public static int[] ROTATIONS = { 0, 1, 2, 3 };

    public const int NUISANCE_DIVISOR = 70;
    public const int SKULL_AMOUNT = 6;

    public const int CELL_EMPTY = 9;
    public const int CELL_SKULL = 0;

    public const int CELL_VISITED = 7;

    public const int TIMEOUT_SAFE_MS = 94;
    public const int TIMEOUT_OPP_MS = 25;

    public const int MAX_GENERATION = 7; // MAX: 7, since 8 is the total max.
    public const int MAX_SIMS_TO_KEEP = 110;
    public const int MIN_COUNTERING_SKULL_LINES = 2;
    public const int MIN_LINES_TO_FIRE = 4;

    public const int MAX_OPP_GENERATION = 3;
    public const int MAX_OPP_SIMS_TO_KEEP = 100;
    public const int MIN_OPP_SKULL_LINES_TO_COUNTER = 3;
    public const int MIN_OPP_LINES_TO_FIRE = 3;

    public const int SCORE_WIN = 50000;

    public const int SCORE_SKULL_DESTROY = 5;
    //public static int[] SKULL_LINE_SCORES = { 0, 100, 200, 400, 700, 1100, 1500, 1800, 2200, 10000 };

    public static void PrintGrid(int[,] grid)
    {
        Logger.Log("");
        var row = new List<string>();

        for (var r = 0; r < GRID_ROWS; r++)
        {
            row.Clear();
            for (var c = 0; c < GRID_COLUMNS; c++)
            {
                var value = grid[r, c];
                if (value == CELL_EMPTY)
                    row.Add("  .");
                else
                    row.Add(value.ToString().PadLeft(3, ' '));
            }

            Logger.Log(string.Join(" ", row));
        }
        Logger.Log("");
    }

    public static void GetInput(int[,] grid)
    {
        //Logger.Log("");
        //var row = new List<string>();

        //for (var r = 0; r < GRID_ROWS; r++)
        //{
        //    row.Clear();
        //    for (var c = 0; c < GRID_COLUMNS; c++)
        //    {
        //        var value = grid[r, c];
        //        if (value == CELL_EMPTY)
        //            row.Add(".");
        //        else
        //            row.Add(value.ToString());
        //    }

        //    Logger.Log($"\"{string.Join("", row)}\",");
        //}
        //Logger.Log("");
    }
}
public static class Logger
{
    public static void Log(string message)
    {
#if DEBUG
        Console.Error.WriteLine(message);
#endif
    }

    public static void DebugLog(string message)
    {
#if DEBUG
        Console.Error.WriteLine(message);
#endif
    }
}
class DummyBot : IBot
{
    Sim _sim;

    bool _timeoutSafe;
    int _generation = 0;

    Sim[] _simulations;
    Sim[] _nextGenSimulations;
    int _simsToKeep;

    int _nextGenIndex;
    Sim _workerSim;
    Sim _passSim;

    Sim _bestSim;

    int _currentMin;
    int _currentMinIndex;

    IComparer<Sim> _comparer;

    public DummyBot()
    {
        _comparer = new OppComparer();
    }

    public void Play(Sim initialSim)
    {
        _sim = initialSim;

        var best = GetBest();
        initialSim.GameResult = best.GameResult;
        //initialSim.OppGrid = best.OppGrid;
        initialSim.OppFitness = best.OppFitness;
        initialSim.OppScore = best.OppScore;
        initialSim.OppTotalTurnScores = best.OppTotalTurnScores;
        initialSim.OppTurnScores = best.OppTurnScores;
        initialSim.OppTotalSkullLines = best.OppTotalSkullLines;
        initialSim.OppSkullLines = best.OppSkullLines;
        initialSim.OppTotalSkullsDestroyed = best.OppTotalSkullsDestroyed;
        initialSim.OppNeighbors = best.OppNeighbors;
        initialSim.OppNuisance = best.OppNuisance;
    }

    Sim GetBest()
    {
        _simulations = new[] { _sim };

        _passSim = null;
        _generation = 0;
        _timeoutSafe = true;
        _currentMinIndex = -1;
        _currentMin = int.MaxValue;

        while (_generation <= Constants.MAX_OPP_GENERATION && _timeoutSafe)
        {
            if (_simulations.Length == 0)
                break;

            if (_generation == 0)
                _simsToKeep = 22;
            else if (_generation == 1)
                _simsToKeep = 484;
            else if (_generation == Constants.MAX_OPP_GENERATION - 1)
                _simsToKeep = 1;
            else
                _simsToKeep = Constants.MAX_OPP_SIMS_TO_KEEP;

            _nextGenSimulations = new Sim[_simsToKeep];
            _nextGenIndex = 0;

            //Logger.Log($"Gen: {_generation}, _simulations = {_simulations.Length}, _simsToKeeP= {_simsToKeep}");

            foreach (var sim in _simulations)
            {
                if (!_timeoutSafe)
                    break;

                foreach (var move in Constants.ROTATIONS)
                {
                    if (!_timeoutSafe)
                        break;

                    for (var c = 0; c < Constants.GRID_COLUMNS; c++)
                    {
                        if (!_timeoutSafe)
                            break;

                        if (c == 0 && move == 2)
                            continue;

                        else if (c == Constants.GRID_COLUMNS - 1 && move == 0)
                            continue;

                        if (sim.Pairs[sim.Depth][0] == sim.Pairs[sim.Depth][1] && move > 1)
                            continue;

                        _workerSim = sim.Copy();

                        Simulator.Simulate(_workerSim, c, move, -1);
                        Evaluate(_workerSim);

                        if (_workerSim.GameResult == 1)
                            continue;

                        if (_nextGenIndex < _simsToKeep)
                        {
                            _nextGenSimulations[_nextGenIndex] = _workerSim;
                            _nextGenIndex++;
                        }
                        else
                        {
                            if (_passSim == null || (_workerSim.OppTurnScores[0] < 40 && _workerSim.OppFitness > 0 && _passSim.OppFitness < _workerSim.OppFitness))
                                _passSim = _workerSim;

                            if (_currentMinIndex == -1)
                                GetMin();

                            if (_currentMin < _workerSim.OppFitness)
                            {
                                _nextGenSimulations[_currentMinIndex] = _workerSim;
                                _currentMinIndex = -1;
                                _currentMin = int.MaxValue;
                            }
                        }

                        CheckTimeout();
                    }

                    CheckTimeout();
                }

                CheckTimeout();
            }

            //Logger.Log($"generation: {_generation} | ms: {SWatch.ElapsedMilliseconds} | sims: {Simulator.Simulations}");

            if (!_timeoutSafe)
                break;

            if (_nextGenIndex == 0 && _bestSim != null)
            {
                return _bestSim;
            }

            if (_nextGenIndex > _simsToKeep)
            {
                _simulations = new Sim[_simsToKeep];
                Array.Copy(_nextGenSimulations, _simulations, _simsToKeep);
            }
            else
            {
                _simulations = new Sim[_nextGenIndex];
                Array.Copy(_nextGenSimulations, _simulations, _nextGenIndex);
            }

            _bestSim = GetMax();
            if (_bestSim != null && _bestSim.OppTotalSkullLines >= Constants.MIN_OPP_LINES_TO_FIRE)
                return _bestSim;

            CheckTimeout();

            _generation++;
        }

        Array.Sort(_simulations, _comparer);
        for (var s = 0; s < _simulations.Length; s++)
        {
            if (_simulations[s].OppTurnScores[0] >= Constants.MIN_OPP_LINES_TO_FIRE)
                return _simulations[s];
            else if (_simulations[s].OppTurnScores[0] < 40)
                return _simulations[s];
        }

        if (_passSim != null)
            return _passSim;

        return _simulations[0];
    }

    void GetMin()
    {
        for (var i = 0; i < _nextGenSimulations.Length; i++)
        {
            if (_nextGenSimulations[i] == null)
                continue;

            if ((_nextGenSimulations[i].OppTotalSkullLines < Constants.MIN_OPP_LINES_TO_FIRE) && (_nextGenSimulations[i].OppFitness < _currentMin || _currentMinIndex == -1))
            {
                _currentMin = _nextGenSimulations[i].OppFitness;
                _currentMinIndex = i;
            }
        }
    }

    Sim GetMax()
    {
        Sim max = null;
        for (var i = 0; i < _simulations.Length; i++)
        {
            if (max == null || max.OppFitness < _simulations[i].OppFitness)
                max = _simulations[i];
        }

        return max;
    }

    void Evaluate(Sim sim)
    {
        sim.OppFitness = sim.OppScore - sim.InitOppScore;

        if (sim.GameResult == -1)
        {
            sim.OppFitness += Constants.SCORE_WIN;
        }
        else if (sim.GameResult == 1)
        {
            sim.OppFitness -= Constants.SCORE_WIN;
        }

        for (var n = 0; n < sim.OppNeighbors.Length; n++)
        {
            sim.OppFitness += (n + 2) * (n + 2) * sim.OppNeighbors[n];
        }

        sim.OppFitness += sim.OppTotalSkullsDestroyed * Constants.SCORE_SKULL_DESTROY;
    }

    void CheckTimeout()
    {
#if !DEBUG
        if (Game.SWatch.ElapsedMilliseconds <= Constants.TIMEOUT_OPP_MS)
            return;

        _timeoutSafe = false;
        Logger.Log($"dummy break at generation {_generation}: {Game.SWatch.ElapsedMilliseconds}");
#endif
    }
}
static class Evaluater
{
    public static void Evaluate(Sim sim)
    {
        sim.MyFitness = (int)(sim.MyTotalTurnScores * Math.Pow(0.7, sim.Depth - 1));

        if (sim.GameResult == 1)
        {
            sim.MyFitness += Constants.SCORE_WIN;
        }
        else if (sim.GameResult == -1)
        {
            sim.MyFitness -= Constants.SCORE_WIN;
        }

        for (var n = 0; n < sim.MyNeighbors.Length; n++)
        {
            sim.MyFitness += (n + 2) * (n + 2) * sim.MyNeighbors[n];
        }

        sim.MyFitness += sim.MyTotalSkullsDestroyed * Constants.SCORE_SKULL_DESTROY;

        var lastAction = sim.Actions[sim.Actions.Count - 1];
        var col = lastAction[0];
        if (col == 2 || col == 3)
            sim.MyFitness += 5;

        if (sim.Pairs[sim.Depth - 1][0] == sim.Pairs[sim.Depth - 1][1])
        {
            var rotation = lastAction[1];
            if (rotation == 1 || rotation == 3)
                sim.MyFitness += 5;
        }

        if (sim.InitMySpaces < 14 && sim.MySpaces > 14 + sim.Depth * 2)
        {
            sim.MyFitness += sim.MySpaces * 5;
        }
        else
        {
            sim.MyFitness -= Constants.MAX_SPACE - sim.MySpaces;
        }

        sim.MyFitness += sim.MyEmptySidedBlocks;
        sim.MyFitness += sim.MyTotalSkullLines * 50;
        sim.MyFitness -= sim.OppFitness;
    }
}
public class Game
{
    public static Stopwatch SWatch = new Stopwatch();

    bool _timeoutSafe;
    int _generation = 0;

    Sim _initialSim;

    Sim[] _simulations;
    Sim[] _nextGenSimulations;
    int _simsToKeep;

    int _nextGenIndex;
    Sim _workerSim;
    Sim _passSim;

    Sim _bestSim;
    Sim _bestFirstSim;

    int _currentMin;
    int _currentMinIndex;

    IBot _dummy;

    public Game()
    {
        _dummy = new DummyBot();
    }

    public string GetBestMove(Sim initialSim)
    {
        Simulator.Simulations = 0;

        _initialSim = initialSim;

        _simulations = new[] { initialSim };

        _passSim = null;
        _generation = 0;
        _timeoutSafe = true;
        _currentMinIndex = -1;
        _currentMin = int.MaxValue;
        _passSim = null;
        _bestFirstSim = null;

        initialSim.CalcNuisance();

        _dummy.Play(initialSim);

        CheckTimeout();

        while (_generation <= Constants.MAX_GENERATION && _timeoutSafe)
        {
            if (_simulations.Length == 0)
                break;

            if (_generation == 0)
                _simsToKeep = 22;
            else if (_generation == 1)
                _simsToKeep = 484;
            else if (_generation == Constants.MAX_GENERATION - 1)
                _simsToKeep = 1;
            else
            {
                if (_initialSim.OppTotalSkullLines > Constants.MIN_OPP_SKULL_LINES_TO_COUNTER)
                    _simsToKeep = 3000;
                else
                    _simsToKeep = Constants.MAX_SIMS_TO_KEEP;
            }

            _nextGenSimulations = new Sim[_simsToKeep];
            _nextGenIndex = 0;

            foreach (var sim in _simulations)
            {
                if (!_timeoutSafe)
                    break;

                CheckTimeout();

                foreach (var move in Constants.ROTATIONS)
                {
                    if (!_timeoutSafe)
                        break;

                    for (var c = 0; c < Constants.GRID_COLUMNS; c++)
                    {
                        if (!_timeoutSafe)
                            break;

                        if (c == 0 && move == 2)
                            continue;

                        else if (c == Constants.GRID_COLUMNS - 1 && move == 0)
                            continue;

                        if (sim.Pairs[sim.Depth][0] == sim.Pairs[sim.Depth][1] && move > 1)
                            continue;

                        _workerSim = sim.Copy();

                        Simulator.Simulate(_workerSim, c, move, 1);
                        Evaluater.Evaluate(_workerSim);

                        if (_workerSim.GameResult == -1)
                            continue;
                        else if (_workerSim.GameResult == 1)
                            return $"{_workerSim.FirstMove[0]} {_workerSim.FirstMove[1]} WON GAME";

                        if (_workerSim.MyTurnScores[0] < 40 && (_passSim == null || _passSim.MyFitness < _workerSim.MyFitness))
                            _passSim = _workerSim;

                        if (_generation == 0)
                        {
                            if (_bestFirstSim == null || _bestFirstSim.MySkullLines[0] < _workerSim.MySkullLines[0])
                                _bestFirstSim = _workerSim;
                        }

                        if (_nextGenIndex < _simsToKeep)
                        {
                            _nextGenSimulations[_nextGenIndex] = _workerSim;
                            _nextGenIndex++;
                        }
                        else
                        {
                            if (_currentMinIndex == -1)
                                GetMin(_workerSim.MyFitness);

                            if (_currentMin < _workerSim.MyFitness)
                            {
                                _nextGenSimulations[_currentMinIndex] = _workerSim;
                                _currentMinIndex = -1;
                                _currentMin = int.MaxValue;
                            }
                        }

                        CheckTimeout();
                    }

                    CheckTimeout();
                }

                CheckTimeout();
            }

            Logger.Log($"generation: {_generation} | ms: {SWatch.ElapsedMilliseconds} | sims: {Simulator.Simulations}");

            if (!_timeoutSafe)
                break;

            if (_nextGenIndex == 0 && _bestSim != null)
            {
                return $"{_bestSim.FirstMove[0]} {_bestSim.FirstMove[1]} NO NEXT GEN... {_bestSim.Depth}: {_bestSim.MyScore}!!";
            }

            if (_nextGenIndex > _simsToKeep)
            {
                _simulations = new Sim[_simsToKeep];
                Array.Copy(_nextGenSimulations, _simulations, _simsToKeep);
            }
            else
            {
                _simulations = new Sim[_nextGenIndex];
                Array.Copy(_nextGenSimulations, _simulations, _nextGenIndex);
            }

            GetBest();
            if (_bestSim != null && _bestSim.MySkullLines[_generation] >= Constants.MIN_LINES_TO_FIRE)
                return $"{_bestSim.FirstMove[0]} {_bestSim.FirstMove[1]} FIRE in {_bestSim.Depth}: {_bestSim.MyScore}!! (F: {_bestSim.MyFitness}| L: {_bestSim.MySkullLines[_generation]})";

            if (_generation <= Constants.MAX_OPP_GENERATION)
            {
                if (_simulations[0] != null && _simulations[0].OppTotalSkullLines >= Constants.MIN_OPP_SKULL_LINES_TO_COUNTER)
                {
                    Logger.Log($"DEF!!! OTL: {_simulations[0].OppTotalSkullLines} | best: {_bestFirstSim.MySkullLines[_generation]}");

                    if (_bestFirstSim != null)
                    {
                        if ((_simulations[0].OppTotalSkullLines == Constants.MIN_OPP_SKULL_LINES_TO_COUNTER && _bestFirstSim.MySkullLines[_generation] >= Constants.MIN_COUNTERING_SKULL_LINES)
                            || ((_simulations[0].OppTotalSkullLines > Constants.MIN_OPP_SKULL_LINES_TO_COUNTER && _bestFirstSim.MySkullLines[_generation] >= 1)))
                        {
                            return $"{_bestFirstSim.FirstMove[0]} {_bestFirstSim.FirstMove[1]} BESTFIRST FIRE!! OF:{_simulations[0].OppFitness}, ML: {_bestSim.MySkullLines[_generation]}";
                        }
                    }
                }
            }

            CheckTimeout();

            _generation++;
        }

        Array.Sort(_simulations);
        for (var s = 0; s < _simulations.Length; s++)
        {
            if (_simulations[s] == null)
                continue;

            if (_simulations[s].MySkullLines[0] >= Constants.MIN_LINES_TO_FIRE)// || (_simulations[s].OppFitness >= Constants.ENEMY_FIRE && _simulations[s].MyFirstTurnScore > Constants.MIN_COUNTER_FIRE_SCORE))
                return $"{_simulations[s].FirstMove[0]} {_simulations[s].FirstMove[1]} 1st:{_simulations[s].MyTurnScores[0]}|F:{_simulations[s].MyFitness}|OF:{_simulations[s].OppFitness}|S:{_simulations[s].MyScore}";
            else if (_simulations[s].MyTurnScores[0] < 40)
                return $"{_simulations[s].FirstMove[0]} {_simulations[s].FirstMove[1]} 1st:{_simulations[s].MyTurnScores[0]}|F:{_simulations[s].MyFitness}|OF:{_simulations[s].OppFitness}|S:{_simulations[s].MyScore}";
        }

        if (_passSim != null)
            return $"{_passSim.FirstMove[0]} {_passSim.FirstMove[1]} continue for best: {_passSim.MyFitness}";

        return $"{_simulations[0].FirstMove[0]} {_simulations[0].FirstMove[1]} NO MOVE";
    }

    void GetMin(int placeFitness)
    {
        for (var i = 0; i < _nextGenSimulations.Length; i++)
        {
            if (_nextGenSimulations[i] == null)
                continue;

            if ((_nextGenSimulations[i].MyFitness < placeFitness) && (_nextGenSimulations[i].MyFitness < _currentMin || _currentMinIndex == -1))
            {
                _currentMin = _nextGenSimulations[i].MyFitness;
                _currentMinIndex = i;
            }
        }
    }

    void GetBest()
    {
        _bestSim = null;
        for (var i = 0; i < _simulations.Length; i++)
        {
            if (_bestSim == null || _bestSim.MyFitness < _simulations[i].MyFitness)
                _bestSim = _simulations[i];
        }
    }

    void CheckTimeout()
    {
#if !DEBUG
        if (SWatch.ElapsedMilliseconds <= Constants.TIMEOUT_SAFE_MS)
            return;

        _timeoutSafe = false;
        Logger.Log($"break: {SWatch.ElapsedMilliseconds}");
#endif
    }
}
public interface IBot
{
    void Play(Sim sim);
}
class OppComparer : IComparer<Sim>
{
    public int Compare(Sim x, Sim y)
    {
        if (x == null && y == null)
            return 0;

        if (x == null)
            return 1;
        if (y == null)
            return -1;

        if (x.OppFitness > y.OppFitness)
            return -1;

        if (x.OppFitness < y.OppFitness)
            return 1;

        return 0;
    }
}
public class Simulator
{
    public static int Simulations = 0;

    static int _turnScore;
    static int _chain;
    static Sim _sim;

    static int[] _pair;
    static int _x1, _x2;

    static int _faction;
    static int[,] _grid;

    static List<int[]> _toSpreadList = new List<int[]>();
    static List<int[]> _spreadingList = new List<int[]>();
    static List<List<int[]>> _stepConnections = new List<List<int[]>>();

    public static void Simulate(Sim sim, int c, int move, int faction)
    {
        Simulations++;

        _faction = faction;

        _sim = sim;
        _sim.MySpaces -= 2;
        _sim.OppSpaces -= 2;
        _sim.MyEmptySidedBlocks = 0;
        _sim.MyNeighbors[0] = 0;
        _sim.MyNeighbors[1] = 0;
        _sim.OppNeighbors[0] = 0;
        _sim.OppNeighbors[1] = 0;

        _pair = new[] { _sim.Pairs[_sim.Depth][0], _sim.Pairs[_sim.Depth][1] };

        if (faction == 1)
            _grid = _sim.MyGrid;
        else
            _grid = sim.OppGrid;

        if (_sim.Depth == 0 && faction == 1)
        {
            _sim.FirstMove = new[] { c, move };
            _sim.Actions.Add($"{c} {move}");
        }

        if (!PlayTurn(c, move))
            OnGameLost(faction);

        if (faction == 1)
        {
            _sim.MyTurnScores[_sim.Depth] = _turnScore;
            _sim.MyTotalTurnScores += _turnScore;
            _sim.MyScore += _turnScore;
            _sim.MyNuisance += (double)_turnScore / Constants.NUISANCE_DIVISOR;
            _sim.MySkullLines[_sim.Depth] = (int)(_sim.MyNuisance / Constants.SKULL_AMOUNT);
            if (_sim.MySkullLines[_sim.Depth] > 0)
            {
                _sim.MyNuisance %= Constants.SKULL_AMOUNT;
                _sim.MyTotalSkullLines += _sim.MySkullLines[_sim.Depth];
            }
        }
        else
        {
            _sim.OppTurnScores[_sim.Depth] = _turnScore;
            _sim.OppTotalTurnScores += _turnScore;
            _sim.OppScore += _turnScore;
            _sim.OppNuisance += (double)_turnScore / Constants.NUISANCE_DIVISOR;
            _sim.OppSkullLines[_sim.Depth] = (int)(_sim.OppNuisance / Constants.SKULL_AMOUNT);
            if (_sim.OppSkullLines[_sim.Depth] > 0)
            {
                _sim.OppNuisance %= Constants.SKULL_AMOUNT;
                _sim.OppTotalSkullLines += _sim.OppSkullLines[_sim.Depth];
            }
        }

        CheckNeighbors();

        _sim.NewTurn();
    }

    static bool PlayTurn(int c, int move)
    {
        _turnScore = 0;
        _chain = 0;

        // TODO: DROP opponent SKULLS

        InitPlacement(c, move);

        if (!FallBlock(_grid, 0, _x1, _pair[0]) || !FallBlock(_grid, 0, _x2, _pair[1]))
            return false;

        while (_toSpreadList.Any())
        {
            GetStepConnections();

            CollapseStep();

            CalcScore();
        }

        return true;
    }

    static void CollapseStep()
    {
        var minMax = new Tuple<int, int>[Constants.GRID_COLUMNS];  // [col] (bot, top)
        foreach (var connection in _stepConnections)
        {
            foreach (var block in connection)
            {
                _grid[block[0], block[1]] = Constants.CELL_EMPTY;
                if (minMax[block[1]] == null)
                    minMax[block[1]] = new Tuple<int, int>(block[0], block[0]);
                else if (minMax[block[1]].Item1 < block[0])
                    minMax[block[1]] = new Tuple<int, int>(block[0], minMax[block[1]].Item2);
                else if (minMax[block[1]].Item2 > block[0])
                    minMax[block[1]] = new Tuple<int, int>(minMax[block[1]].Item2, block[0]);
            }
        }

        if (!_stepConnections.Any())
            return;

        for (var c = 0; c < Constants.GRID_COLUMNS; c++)
        {
            if (minMax[c] == null)
                continue;

            if (minMax[c].Item1 == 0)
                continue;

            for (var r = minMax[c].Item1 - 1; r >= 0; r--)
            {
                var block = _grid[r, c];
                if (block != Constants.CELL_EMPTY)
                {
                    _grid[r, c] = Constants.CELL_EMPTY;
                    FallBlock(_grid, r + 1, c, block);
                }
                else if (block == Constants.CELL_EMPTY
                    && r < minMax[c].Item2 - 1)
                    break;
            }
        }
    }

    static bool FallBlock(int[,] grid, int row, int col, int value)
    {
        for (var r = row; r < Constants.GRID_ROWS; r++)
        {
            if (grid[r, col] == Constants.CELL_EMPTY)
                continue;

            if (r == 0)
                return false;

            grid[r - 1, col] = value;
            _toSpreadList.Add(new int[] { r - 1, col, value });
            return true;
        }

        grid[Constants.GRID_ROWS - 1, col] = value;
        _toSpreadList.Add(new int[] { Constants.GRID_ROWS - 1, col, value });
        return true;
    }

    static void GetStepConnections()
    {
        _stepConnections.Clear();

        var clone = (int[,])_grid.Clone();

        for (var i = 0; i < _toSpreadList.Count; i++)
        {
            var toSpread = _toSpreadList[i];
            if (toSpread[2] == Constants.CELL_SKULL)
                continue;

            Spread(clone, toSpread[0], toSpread[1], toSpread[2]);

            var count = 0;
            for (var colorCount = 0; colorCount < _spreadingList.Count; colorCount++)
            {
                if (_spreadingList[colorCount][2] != Constants.CELL_SKULL)
                    count++;
            }

            if (count >= Constants.BLOCKS_TO_SCORE)
                _stepConnections.Add(_spreadingList.ToList());
            else
                OnNeighborsFound(count);

            _spreadingList.Clear();
        }

        _toSpreadList.Clear();
    }

    static void Spread(int[,] grid, int row, int col, int value)
    {
        if (value == Constants.CELL_VISITED
            || value == Constants.CELL_EMPTY
            || value == Constants.CELL_SKULL)
            return;

        _spreadingList.Add(new int[] { row, col, value });

        if (value != Constants.CELL_SKULL)
            grid[row, col] = Constants.CELL_VISITED;

        if (col != Constants.GRID_COLUMNS - 1)
        {
            var right = grid[row, col + 1];
            if (right == value)
                Spread(grid, row, col + 1, right);
            else if (right == Constants.CELL_SKULL && !IsSpreading(row, col + 1))
                _spreadingList.Add(new int[] { row, col + 1, Constants.CELL_SKULL });
        }

        if (row != 0)
        {
            var top = grid[row - 1, col];
            if (top == value)
                Spread(grid, row - 1, col, top);
            else if (top == Constants.CELL_SKULL && !IsSpreading(row - 1, col))
                _spreadingList.Add(new int[] { row - 1, col, Constants.CELL_SKULL });
        }

        if (col != 0)
        {
            var left = grid[row, col - 1];
            if (left == value)
                Spread(grid, row, col - 1, left);
            else if (left == Constants.CELL_SKULL && !IsSpreading(row, col - 1))
                _spreadingList.Add(new int[] { row, col - 1, Constants.CELL_SKULL });
        }

        if (row != Constants.GRID_ROWS - 1)
        {
            var bot = grid[row + 1, col];
            if (bot == value)
                Spread(grid, row + 1, col, bot);
            else if (bot == Constants.CELL_SKULL && !IsSpreading(row + 1, col))
                _spreadingList.Add(new int[] { row + 1, col, Constants.CELL_SKULL });
        }
    }

    static bool IsSpreading(int row, int col)
    {
        for (var i = 0; i < _spreadingList.Count; i++)
        {
            if (_spreadingList[i][0] == row
                && _spreadingList[i][1] == col)
                return true;
        }

        return false;
    }

    static void InitPlacement(int c, int move)
    {
        switch (move)
        {
            case 0:
                _x1 = c;
                _x2 = c + 1;
                break;

            case 1:
                _x1 = c;
                _x2 = c;
                break;

            case 2:
                _x1 = c;
                _x2 = c - 1;
                break;

            case 3:
                _x1 = c;
                _x2 = c;
                var temp = _pair[0];
                _pair[0] = _pair[1];
                _pair[1] = temp;
                break;
        }
    }

    static void CalcScore()
    {
        // score = (10 * B) * (CP + CB + GB)

        if (!_stepConnections.Any())
            return;

        var B = 0;
        var colors = new bool[5];
        var skulls = 0;

        for (var i = 0; i < _stepConnections.Count; i++)
        {
            var connection = _stepConnections[i];
            for (var j = 0; j < connection.Count; j++)
            {
                if (connection[j][2] != Constants.CELL_SKULL)
                {
                    colors[connection[j][2] - 1] = true;
                    B++;
                }
                else if (connection[j][2] == Constants.CELL_SKULL)
                    skulls++;
            }
        }

        _sim.MySpaces += B;
        _sim.MySpaces += skulls;

        if (_faction == 1)
        {
            _sim.MyTotalSkullsDestroyed += skulls;
        }
        else
        {
            _sim.OppTotalSkullsDestroyed += skulls;
        }

        var blockScore = 10 * B;

        var CP = 0;
        if (_chain >= 1)
        {
            for (var i = 1; i <= _chain; i++)
            {
                if (i == 1)
                    CP = 8;
                else
                    CP *= 2;
            }
        }

        var CB = -1;
        for (var i = 0; i < colors.Length; i++)
        {
            if (colors[i])
            {
                if (CB == -1)
                    CB = 0;
                else if (CB == 0)
                    CB = 2;
                else
                    CB *= 2;
            }
        }

        var GB = 0;
        for (var i = 5; i <= B; i++)
        {
            GB += 1;
            //if (GB == 8)
            //    break;
        }

        if (_stepConnections.Count > 1)
            GB -= _stepConnections.Count * Constants.BLOCKS_TO_SCORE;

        var multiplier = (CP + CB + GB);
        if (multiplier > 999)
            multiplier = 999;
        else if (multiplier < 1)
            multiplier = 1;

        _turnScore += blockScore * multiplier;
        _chain++;
    }

    static void CheckNeighbors()
    {
        _toSpreadList.Clear();

        bool shouldCheck = false;

        for (var r = 0; r < Constants.GRID_ROWS; r++)
        {
            for (var c = 0; c < Constants.GRID_COLUMNS; c++)
            {
                shouldCheck = false;

                if (_grid[r, c] == Constants.CELL_EMPTY
                    || _grid[r, c] == Constants.CELL_VISITED
                    || _grid[r, c] == Constants.CELL_SKULL)
                    continue;

                if (r > 0)
                {
                    if (_grid[r - 1, c] == Constants.CELL_EMPTY)
                    {
                        shouldCheck = true;
                        if (_faction == 1)
                            _sim.MyEmptySidedBlocks++;
                    }
                }

                if (r < Constants.GRID_ROWS - 2)
                {
                    if (_grid[r + 1, c] == Constants.CELL_EMPTY)
                    {
                        shouldCheck = true;
                        _sim.MyEmptySidedBlocks++;
                    }
                }

                if (c > 0)
                {
                    if (_grid[r, c - 1] == Constants.CELL_EMPTY)
                    {
                        shouldCheck = true;
                        _sim.MyEmptySidedBlocks++;
                    }
                }

                if (shouldCheck)
                    _toSpreadList.Add(new[] { r, c, _grid[r, c] });
            }
        }

        GetStepConnections();
    }

    static void OnNeighborsFound(int neighbor)
    {
        if (neighbor <= 1)
            return;

        if (_faction == 1)
            _sim.MyNeighbors[neighbor - 2]++;
        else
            _sim.OppNeighbors[neighbor - 2]++;
    }

    static void OnGameLost(int loserFaction)
    {
        _sim.GameResult = -loserFaction;
    }
}
public class Sim : IComparable<Sim>
{
    internal int GameResult { get; set; }

    internal int Depth { get; set; }

    internal int MyFitness { get; set; }
    internal int OppFitness { get; set; }

    internal int[] FirstMove { get; set; }

    internal int MyTotalTurnScores { get; set; }
    internal int[] MyTurnScores { get; set; }
    internal int MyTotalSkullLines { get; set; }
    internal int[] MySkullLines { get; set; }
    internal int MyTotalSkullsDestroyed { get; set; }
    //internal int[] MySkullsDestroyed { get; set; }
    internal int MyEmptySidedBlocks { get; set; }
    internal int[] MyNeighbors { get; set; } // 0.: 2 conn, 1.: 3 conn
    internal double MyNuisance { get; set; }

    internal int OppTotalTurnScores { get; set; }
    internal int[] OppTurnScores { get; set; }
    internal int OppTotalSkullLines { get; set; }
    internal int[] OppSkullLines { get; set; }
    internal int OppTotalSkullsDestroyed { get; set; }
    internal int[] OppNeighbors { get; set; }
    internal double OppNuisance { get; set; }

    internal int InitMyScore { get; set; }
    internal int InitMySpaces { get; set; }
    internal int InitOppScore { get; set; }
    internal int InitOppSpaces { get; set; }

    public int Turn { get; set; }
    public int[][] Pairs { get; internal set; }

    public int[,] MyGrid { get; internal set; }
    public int[,] OppGrid { get; internal set; }

    public int MySpaces { get; set; }
    public int OppSpaces { get; set; }
    public int OppMaxToFillWithSkulls { get; set; }

    public int MyScore { get; internal set; }
    public int OppScore { get; internal set; }

    internal List<string> Actions { get; set; }

    public Sim()
    {

    }

    public Sim(int turn, int[][] pairs, int[,] myGrid, int[,] oppGrid, int myScore, int oppScore, int mySpaces, int oppSpaces)
    {
        Turn = turn;
        Pairs = pairs;

        MyGrid = myGrid;
        OppGrid = oppGrid;

        MyScore = myScore;
        InitMyScore = myScore;
        OppScore = oppScore;
        InitOppScore = oppScore;

        MySpaces = mySpaces;
        InitMySpaces = mySpaces;
        OppSpaces = oppSpaces;
        InitOppSpaces = oppSpaces;

        OppNeighbors = new int[2];

        MyTurnScores = new int[Constants.ADVANCE_TURNS];
        //MySkullsDestroyed = new int[Constants.ADVANCE_TURNS];
        MySkullLines = new int[Constants.ADVANCE_TURNS];
        MyNeighbors = new int[2];

        OppTurnScores = new int[Constants.ADVANCE_TURNS];
        //MySkullsDestroyed = new int[Constants.ADVANCE_TURNS];
        OppSkullLines = new int[Constants.ADVANCE_TURNS];
        OppNeighbors = new int[2];

        Actions = new List<string>();
    }

    internal void NewTurn()
    {
        Turn++;
        Depth++;
    }

    public Sim Copy()
    {
        var myCopy = (int[,])MyGrid.Clone();
        var oppCopy = (int[,])OppGrid.Clone();
        var copy = new Sim()
        {
            Turn = Turn,
            Pairs = Pairs,

            MyGrid = myCopy,
            OppGrid = oppCopy,

            MyScore = MyScore,
            OppScore = OppScore,

            InitMyScore = InitMyScore,
            InitMySpaces = InitMySpaces,

            InitOppScore = InitOppScore,
            InitOppSpaces = InitOppSpaces,

            GameResult = GameResult,
            Depth = Depth,

            FirstMove = FirstMove,
            OppMaxToFillWithSkulls = OppMaxToFillWithSkulls,

            MyTotalTurnScores = MyTotalTurnScores,
            MyTurnScores = (int[])MyTurnScores.Clone(),
            MyTotalSkullLines = MyTotalSkullLines,
            MySkullLines = (int[])MySkullLines.Clone(),
            MyTotalSkullsDestroyed = MyTotalSkullsDestroyed,
            MyNeighbors = (int[])MyNeighbors.Clone(),
            MyNuisance = MyNuisance,

            OppTotalTurnScores = OppTotalTurnScores,
            OppTurnScores = (int[])OppTurnScores.Clone(),
            OppTotalSkullLines = OppTotalSkullLines,
            OppSkullLines = (int[])OppSkullLines.Clone(),
            OppTotalSkullsDestroyed = OppTotalSkullsDestroyed,
            OppNeighbors = (int[])OppNeighbors.Clone(),
            OppNuisance = OppNuisance,

            Actions = Actions.ToList()
        };
        return copy;
    }

    internal void CalcNuisance()
    {
        MyNuisance = (double)MyScore / Constants.NUISANCE_DIVISOR;
        MyNuisance %= Constants.SKULL_AMOUNT;

        OppNuisance = (double)OppScore / Constants.NUISANCE_DIVISOR;
        OppNuisance %= Constants.SKULL_AMOUNT;
    }

    public int CompareTo(Sim other)
    {
        if (other == null)
            return -1;

        if (MyFitness > other.MyFitness)
            return -1;

        if (MyFitness < other.MyFitness)
            return 1;

        return 0;
    }

    public override string ToString()
    {
        return $"{FirstMove[0]} {FirstMove[1]} {MyFitness}";
    }
}
