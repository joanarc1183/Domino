using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoGame
{
    public class Program
    {
        public static void Main()
        {
            Console.Write("Jumlah player: ");
            int count = int.Parse(Console.ReadLine()!);
            
            var players = new List<Player>();

            for (int i = 0; i < count; i++)
            {
                Console.Write($"Nama player {i + 1}: ");
                string name = Console.ReadLine()!;
                players.Add(new Player(name));
            }

            IBoard board = new Board();
            var game = new GameController(players, board, 50);

            var ui = new ConsoleGameUI(game);
            
            game.OnGameEnded += winner =>
            {
                Console.WriteLine("\n=== GAME OVER ===");
                Console.WriteLine($"🏆 Winner: {winner.Name}");
            };

            // ================= GAME LOOP =================
            game.StartRound();

            while (!game.IsGameEnded)
            {
                while (!game.IsRoundEnded)
                {
                    ui.HandleTurn();
                }

                if (!game.IsGameEnded)
                {
                    Console.WriteLine("\nPress any key to start next round...");
                    Console.ReadKey();
                    game.StartRound();
                }
            }
        }

    }
}
