using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoGame
{
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
                // kalau setelah pass langsung round ended, jangan spam pesan
                if (_roundEnded)
                    return;

            CheckRoundEnd();
            MoveNextPlayer();
        }

        // ================= TURN HANDLING =================

        private void HandlePlay(IPlayer player)
        {
            var playableIndexes = GetPlayableDominoIndexes(player);

            // 🚫 Kalau benar-benar tidak ada kartu valid → pass
            if (playableIndexes.Count == 0)
            {
                Console.WriteLine($"{player.Name} has no playable dominoes.");
                HandlePass(player);
                return;
            }

            Console.WriteLine("\nChoose a domino index to play:");

            for (int i = 0; i < player.Hand.Count; i++)
                Console.WriteLine($"{i}. {player.Hand[i]}");

            while (true)
            {
                Console.Write("> ");
                if (!int.TryParse(Console.ReadLine(), out int choice))
                {
                    Console.WriteLine("Invalid input.");
                    continue;
                }

                // ❌ player mencoba pass padahal bisa main
                if (choice == -1)
                {
                    Console.WriteLine("You have playable dominoes. You must play one.");
                    ShowPlayableDominoes(player, playableIndexes);
                    continue;
                }

                if (choice < 0 || choice >= player.Hand.Count)
                {
                    Console.WriteLine("Index out of range.");
                    continue;
                }

                // ❌ domino tidak bisa dimainkan
                if (!playableIndexes.Contains(choice))
                {
                    Console.WriteLine("That domino cannot be played.");
                    ShowPlayableDominoes(player, playableIndexes);
                    continue; // 🔁 ulang input
                }

                // ✅ domino valid
                var domino = player.Hand[choice];

                // Board masih kosong → langsung taruh
                if (_board.IsEmpty)
                {
                    _board.Place(domino, BoardSide.Left);
                }
                else
                {
                    bool canLeft = _board.CanPlace(domino, BoardSide.Left);
                    bool canRight = _board.CanPlace(domino, BoardSide.Right);

                    BoardSide side = (canLeft && canRight)
                        ? AskSide()
                        : (canLeft ? BoardSide.Left : BoardSide.Right);

                    _board.Place(domino, side);
                }

                player.Hand.RemoveAt(choice);
                _consecutivePasses = 0;
                ActionExecuted?.Invoke(player, EventArgs.Empty);
                break;
            }
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

        private List<int> GetPlayableDominoIndexes(IPlayer player)
        {
            var playable = new List<int>();

            for (int i = 0; i < player.Hand.Count; i++)
            {
                var d = player.Hand[i];

                if (_board.IsEmpty ||
                    _board.CanPlace(d, BoardSide.Left) ||
                    _board.CanPlace(d, BoardSide.Right))
                {
                    playable.Add(i);
                }
            }

            return playable;
        }

        private void ShowPlayableDominoes(IPlayer player, List<int> playableIndexes)
        {
            Console.WriteLine("Playable dominoes:");
            foreach (var i in playableIndexes)
                Console.WriteLine($"  {i}. {player.Hand[i]}");
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

                RenderHandsAtRoundEnd();
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

        private void RenderHandsAtRoundEnd()
        {
            Console.WriteLine("\nHands at round end:");
            foreach (var p in _controller.Players)
            {
                Console.Write($"{p.Name}: ");
                if (p.Hand.Count == 0)
                {
                    Console.WriteLine("(empty)");
                }
                else
                {
                    foreach (var d in p.Hand)
                        Console.Write($"{d} ");
                    Console.WriteLine();
                }
            }
        }

    }
}
