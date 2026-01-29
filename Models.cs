using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoGame
{
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
        public string Name { get; }
        public int Score { get; set; }
        public List<IDomino> Hand { get; } = new();

        public Player(string name)
        {
            Name = name;
        }
    }
}
