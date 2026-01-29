using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoGame
{
    // ================= ENUMS =================
    public enum Dot { Blank, One, Two, Three, Four, Five, Six }
    public enum DominoOrientation { Horizontal, Vertical }
    public enum BoardSide { Left, Right }

    // ================= INTERFACES =================
    public interface IDomino
    {
        Dot LeftPip { get; }
        Dot RightPip { get; }
        DominoOrientation Orientation { get; set; }
        bool IsDouble();
        bool CanConnect(IDomino other);
    }

    public interface IBoneyard
    {
        List<IDomino> Dominoes { get; }
        IDomino Draw();
        bool IsEmpty { get; }
    }

    public interface IBoard
    {
        LinkedList<IDomino> Dominoes { get; }
        bool IsEmpty { get; }
        Dot LeftEnd { get; }
        Dot RightEnd { get; }
        bool CanPlace(IDomino domino);
        bool CanPlace(IDomino domino, BoardSide side);
        void Place(IDomino domino, BoardSide side);
        void Reset();
    }

    public interface IPlayer
    {
        int Id { get; }
        string Name { get; }
        int Score { get; set; }
        List<IDomino> Hand { get; }
    }

    public interface IConsoleRenderer
    {
        void Start();
    }

    // ================= IMPLEMENTATIONS =================

    public sealed class Domino : IDomino
    {
        public Dot LeftPip { get; private set; }
        public Dot RightPip { get; private set; }
        public DominoOrientation Orientation { get; set; }

        public Domino(Dot left, Dot right)
        {
            LeftPip = left;
            RightPip = right;
            Orientation = DominoOrientation.Horizontal;
        }

        public bool IsDouble() => LeftPip == RightPip;

        public bool CanConnect(IDomino other)
        {
            return LeftPip == other.LeftPip ||
                   LeftPip == other.RightPip ||
                   RightPip == other.LeftPip ||
                   RightPip == other.RightPip;
        }

        public Domino Flip()
        {
            return new Domino(RightPip, LeftPip) { Orientation = Orientation };
        }

        public override string ToString() => $"[{LeftPip}|{RightPip}]";
    }

    public class Boneyard : IBoneyard
    {
        public List<IDomino> Dominoes { get; private set; } = new();
        public bool IsEmpty => Dominoes.Count == 0;

        public Boneyard(IEnumerable<IDomino> set)
        {
            Dominoes = set.ToList();
            Shuffle();
        }

        public IDomino Draw()
        {
            if (IsEmpty) throw new InvalidOperationException("Boneyard empty");
            var d = Dominoes[0];
            Dominoes.RemoveAt(0);
            return d;
        }

        private void Shuffle()
        {
            var rnd = new Random();
            Dominoes = Dominoes.OrderBy(_ => rnd.Next()).ToList();
        }
    }

    public class Board : IBoard
    {
        public LinkedList<IDomino> Dominoes { get; private set; } = new();
        public bool IsEmpty => Dominoes.Count == 0;
        public Dot LeftEnd => Dominoes.First!.Value.LeftPip;
        public Dot RightEnd => Dominoes.Last!.Value.RightPip;

        public bool CanPlace(IDomino domino)
        {
            if (IsEmpty) return true;
            return CanPlace(domino, BoardSide.Left) || CanPlace(domino, BoardSide.Right);
        }

        public bool CanPlace(IDomino domino, BoardSide side)
        {
            if (IsEmpty) return true;
            return side == BoardSide.Left
                ? domino.LeftPip == LeftEnd || domino.RightPip == LeftEnd
                : domino.LeftPip == RightEnd || domino.RightPip == RightEnd;
        }

        public void Place(IDomino domino, BoardSide side)
        {
            if (!CanPlace(domino, side))
                throw new InvalidOperationException("Invalid placement");

            if (IsEmpty)
            {
                Dominoes.AddFirst(domino);
                return;
            }

            if (side == BoardSide.Left)
            {
                if (domino.RightPip == LeftEnd)
                    Dominoes.AddFirst(domino);
                else
                    Dominoes.AddFirst(((Domino)domino).Flip());
            }
            else
            {
                if (domino.LeftPip == RightEnd)
                    Dominoes.AddLast(domino);
                else
                    Dominoes.AddLast(((Domino)domino).Flip());
            }
        }

        public void Reset() => Dominoes.Clear();
    }

    public class Player : IPlayer
    {
        public int Id { get; }
        public string Name { get; }
        public int Score { get; set; }
        public List<IDomino> Hand { get; } = new();

        public Player(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    // ================= GAME CONTROLLER (NO INTERFACE) =================

    public class GameController
    {
        public event EventHandler? TurnStarted;
        public event EventHandler? ActionExecuted;
        public event EventHandler? RoundEnded;
        public event EventHandler? GameEnded;

        private readonly List<IPlayer> _players = new();
        private readonly IBoard _board;
        private IBoneyard _boneyard;
        private int _currentPlayerIndex;
        private int _roundLeaderIndex = -1;
        private int _consecutivePasses;
        private bool _roundEnded;
        private readonly int _maxScoreToWin;

        public bool IsRoundEnded => _roundEnded;
        public bool IsGameEnded { get; private set; }
        public IPlayer? GameWinner { get; private set; }

        public IEnumerable<IDomino> BoardDominoes => _board.Dominoes;
        public bool BoardEmpty => _board.IsEmpty;
        public IEnumerable<IPlayer> Players => _players;

        public GameController(int maxScoreToWin)
        {
            _maxScoreToWin = maxScoreToWin;
            _board = new Board();
            _boneyard = new Boneyard(GenerateFullSet());
        }

        public void AddPlayer(IPlayer player) => _players.Add(player);

        public IPlayer GetCurrentPlayer() => _players[_currentPlayerIndex];

        public void StartRound()
        {
            ResetRound();
            DecideFirstPlayer();
            DealInitialHands();
        }

        public void PlayerAction()
        {
            if (_roundEnded || IsGameEnded) return;

            var player = GetCurrentPlayer();
            TurnStarted?.Invoke(player, EventArgs.Empty);

            if (CanPlay(player))
                HandlePlay(player);
            else
                HandlePass(player);

            CheckRoundEnd();
            MoveNextPlayer();
        }

        // ================= TURN HANDLING =================

        private void HandlePlay(IPlayer player)
        {
            Console.WriteLine("\nChoose a domino index to play (-1 to pass):");

            for (int i = 0; i < player.Hand.Count; i++)
                Console.WriteLine($"{i}. {player.Hand[i]}");

            int choice;
            while (!int.TryParse(Console.ReadLine(), out choice) ||
                (choice < -1 || choice >= player.Hand.Count))
            {
                Console.Write("Invalid input. Choose again: ");
            }

            if (choice == -1)
            {
                HandlePass(player);
                return;
            }

            var domino = player.Hand[choice];

            bool canLeft = _board.CanPlace(domino, BoardSide.Left);
            bool canRight = _board.CanPlace(domino, BoardSide.Right);

            if (!canLeft && !canRight)
            {
                Console.WriteLine("That domino cannot be placed. You lose your turn.");
                HandlePass(player);
                return;
            }

            BoardSide side;

            if (canLeft && canRight)
            {
                if (_board.IsEmpty)
                {
                    side = BoardSide.Left;
                } else
                {
                    side = AskSide();
                }
            }
            else if (canLeft)
            {
                side = BoardSide.Left;
            }
            else
            {
                side = BoardSide.Right;
            }

            _board.Place(domino, side);

            player.Hand.Remove(domino);
            _consecutivePasses = 0;

            ActionExecuted?.Invoke(player, EventArgs.Empty);
        }

        private void HandlePass(IPlayer player)
        {
            // RULE: no draw, just pass
            Console.WriteLine($"{player.Name} cannot play and is skipped.");
            _consecutivePasses++;
        }

        public void SortPlayersByName()
        {
            _players.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        private BoardSide AskSide()
        {
            Console.Write("Place on Left or Right? (L/R): ");
            while (true)
            {
                var input = Console.ReadLine()!.Trim().ToUpper();
                if (input == "L") return BoardSide.Left;
                if (input == "R") return BoardSide.Right;

                Console.Write("Invalid input. Enter L or R: ");
            }
        }



        // ================= ROUND END =================

        private void CheckRoundEnd()
        {
            // Normal win
            var emptyPlayer = _players.FirstOrDefault(p => p.Hand.Count == 0);
            if (emptyPlayer != null)
            {
                _roundEnded = true;
                HandleNormalWin(emptyPlayer);
                return;
            }

            // Blocked game
            if (_consecutivePasses >= _players.Count)
            {
                _roundEnded = true;
                HandleBlockedGame();
            }
        }

        private void HandleNormalWin(IPlayer winner)
        {
            int score = _players
                .Where(p => p != winner)
                .SelectMany(p => p.Hand)
                .Sum(d => (int)d.LeftPip + (int)d.RightPip);

            winner.Score += score;
            RoundEnded?.Invoke(winner, EventArgs.Empty);
            CheckGameEnd();
        }

        private void HandleBlockedGame()
        {
            var pipTotals = _players.ToDictionary(
                p => p,
                p => CountPips(p.Hand)
            );

            int min = pipTotals.Min(x => x.Value);
            var lowestPlayers = pipTotals.Where(x => x.Value == min).ToList();

            // Tie → no winner
            if (lowestPlayers.Count > 1)
            {
                RoundEnded?.Invoke(null, EventArgs.Empty);
                return;
            }

            var winner = lowestPlayers.First().Key;

            int others = pipTotals
                .Where(x => x.Key != winner)
                .Sum(x => x.Value);

            winner.Score += others - pipTotals[winner];
            RoundEnded?.Invoke(winner, EventArgs.Empty);
            CheckGameEnd();
        }

        // ================= GAME END =================

        private void CheckGameEnd()
        {
            GameWinner = _players.FirstOrDefault(p => p.Score >= _maxScoreToWin);
            if (GameWinner != null)
            {
                IsGameEnded = true;
                GameEnded?.Invoke(GameWinner, EventArgs.Empty);
            }
        }

        // ================= SETUP =================

        private void DecideFirstPlayer()
        {
            if (_roundLeaderIndex == -1)
                _roundLeaderIndex = new Random().Next(_players.Count);
            else
                _roundLeaderIndex = (_roundLeaderIndex + 1) % _players.Count;

            _currentPlayerIndex = _roundLeaderIndex;
        }

        private void ResetRound()
        {
            _board.Reset();
            _boneyard = new Boneyard(GenerateFullSet());
            _consecutivePasses = 0;
            _roundEnded = false;

            foreach (var p in _players)
                p.Hand.Clear();
        }

        private void DealInitialHands()
        {
            for (int i = 0; i < 7; i++)
                foreach (var p in _players)
                    p.Hand.Add(_boneyard.Draw());
        }

        private bool CanPlay(IPlayer player) => player.Hand.Any(d => _board.CanPlace(d));

        private void MoveNextPlayer() => _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;

        private int CountPips(IEnumerable<IDomino> dominoes) => dominoes.Sum(d => (int)d.LeftPip + (int)d.RightPip);

        private IEnumerable<IDomino> GenerateFullSet()
        {
            for (int i = 0; i <= 6; i++)
                for (int j = i; j <= 6; j++)
                    yield return new Domino((Dot)i, (Dot)j);
        }
    }

    // ================= CONSOLE RENDERER =================

    public class ConsoleRenderer : IConsoleRenderer
    {
        private readonly GameController _controller;

        public ConsoleRenderer(GameController controller)
        {
            _controller = controller;
            Subscribe();
        }

        public void Start()
        {
            while (!_controller.IsGameEnded)
            {
                _controller.StartRound();

                while (!_controller.IsRoundEnded)
                    _controller.PlayerAction();

                Console.WriteLine("\n=== ROUND ENDED ===");
                Console.ReadKey();
            }
        }


        private void Subscribe()
        {
            // _controller.TurnStarted += (s, e) =>
            //     Console.WriteLine($"Turn: {((IPlayer)s!).Name}");

            _controller.ActionExecuted += (s, e) =>
            {
                Console.WriteLine($"{((IPlayer)s!).Name} played a domino");
                RenderBoard();
            };

            _controller.TurnStarted += (s, e) =>
            {
                Console.Clear();
                Console.WriteLine($"Turn: {((IPlayer)s!).Name}");
                RenderBoard();
            };

            _controller.RoundEnded += (s, e) =>
            {
                if (s is IPlayer winner)
                    Console.WriteLine($"Round winner: {winner.Name}");
                else
                    Console.WriteLine("Round ended in a tie.");

                RenderScores();
            };


            _controller.GameEnded += (s, e) =>
                Console.WriteLine($"Winner: {((IPlayer)s!).Name}");
        }

        private void RenderBoard()
        {
            Console.Write("Board: ");
            if (_controller.BoardEmpty)
            {
                Console.WriteLine("(empty)");
                return;
            }

            foreach (var d in _controller.BoardDominoes)
                Console.Write($"{d} ");
            Console.WriteLine();
        }

        private void RenderScores()
        {
            Console.WriteLine("\nCurrent Scores:");
            foreach (var p in _controller.Players)
                Console.WriteLine($"{p.Name} : {p.Score} points");
        }


        private void RenderHand(IPlayer player)
        {
            Console.WriteLine("Your hand:");
            for (int i = 0; i < player.Hand.Count; i++)
                Console.WriteLine($"{i}. {player.Hand[i]}");
        }
    }
}
