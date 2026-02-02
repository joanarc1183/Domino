using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoGame
{
    public interface IDomino
    {
        Dot LeftPip { get; }
        Dot RightPip { get; }
        DominoOrientation Orientation { get; set; }
        bool IsDouble();
        bool CanConnect(IDomino other);
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

    public interface IConsoleRenderer
    {
        void Start();
    }
}