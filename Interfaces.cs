using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoGame
{
    public interface IBoard
    {
        LinkedList<Domino> Dominoes { get; }
        bool IsEmpty { get; }
        Dot LeftEnd { get; }
        Dot RightEnd { get; }
        bool CanPlace(Domino domino);
        bool CanPlace(Domino domino, BoardSide side);
        void Place(Domino domino, BoardSide side);
        void Reset();
    }

    public interface IConsoleRenderer
    {
        void Start();
    }
}