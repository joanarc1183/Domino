using System;


namespace DominoGame
{
    public class ConsoleGameUI
    {
        private readonly GameController _game;

        public ConsoleGameUI(GameController game)
        {
            _game = game;
            _game.OnRoundEnded += OnRoundEnded;

        }

        // ================= RENDER =================
        private void RenderBoard()
        {
            Console.Write("Board: ");
            if (_game.Board.IsEmpty)
            {
                Console.WriteLine("(empty)");
                return;
            }

            foreach (var d in _game.Board.Dominoes)
                Console.Write($"{d} ");
            Console.WriteLine();
        }

        public void RenderHands(Player player)
        {
            Console.WriteLine($"\n{player.Name}'s hand:");
            var hand = _game.GetHands(player);

            for (int i = 0; i < hand.Count; i++)
                Console.WriteLine($"{i}. {hand[i]}");
        }

        // ================= TURN HANDLING =================
        public void HandleTurn()
        {
            var player = _game.CurrentPlayer;

            Console.WriteLine($"\n=== {player.Name}'s Turn ===");
            RenderBoard();

            if (!_game.CanPlay(player))
            {
                Console.WriteLine($"{player.Name} cannot play and passes.");
                _game.PassTurn(player);
                _game.NextTurn();
                return;
            }

            HandlePlay(player);
            _game.NextTurn();
        }

        private void HandlePlay(Player player)
        {
            var hand = _game.GetHands(player);

            RenderHands(player);

            while (true)
            {
                Console.Write("Choose domino index: ");
                if (!int.TryParse(Console.ReadLine(), out int index))
                {
                    Console.WriteLine("Invalid input.");
                    continue;
                }

                if (index < 0 || index >= hand.Count)
                {
                    Console.WriteLine("Index out of range.");
                    continue;
                }

                var domino = hand[index];

                BoardSide side = AskSideIfNeeded(domino);

                if (_game.PlayDomino(player, domino, side))
                    break;

                Console.WriteLine("That domino cannot be played there.");
            }
        }
        private BoardSide AskSideIfNeeded(Domino domino)
        {
            if (_game.Board.IsEmpty)
                return BoardSide.Left;

            bool left = _game.Board.CanPlace(domino, BoardSide.Left);
            bool right = _game.Board.CanPlace(domino, BoardSide.Right);

            if (left && right)
                return AskSide();

            return left ? BoardSide.Left : BoardSide.Right;
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

        // private void ShowPlayableDominoes(Player player, List<int> playableIndexes)
        // {
        //     Console.WriteLine("Playable dominoes:");
        //     foreach (var i in playableIndexes)
        //         Console.WriteLine($"  {i}. {player.Hand[i]}");
        // }

        private void OnRoundEnded(
            Player? winner,
            bool blocked,
            IReadOnlyDictionary<Player, IReadOnlyList<Domino>> hands)
        {
            Console.WriteLine("\n=== ROUND ENDED ===");

            if (winner == null)
                Console.WriteLine("Round ended in a tie (blocked game).");
            else
                Console.WriteLine($"Winner: {winner.Name} {(blocked ? "(blocked game)" : "")}");

            Console.WriteLine("\nHands at round end:");
            foreach (var (player, hand) in hands)
            {
                Console.Write($"{player.Name}: ");
                if (hand.Count == 0)
                    Console.WriteLine("(empty)");
                else
                {
                    foreach (var d in hand)
                        Console.Write($"{d} ");
                    Console.WriteLine();
                }
            }

            Console.WriteLine("\nScores:");
            foreach (var p in _game.Players)
                Console.WriteLine($"{p.Name}: {p.Score}");
        }

    
    }
}