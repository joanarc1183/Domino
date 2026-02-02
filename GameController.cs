using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoGame
{
    public class GameController
    {
        // ================= EVENTS =================
        public event Action<Player>? OnTurnChanged;
        public event Action<Player, Domino, BoardSide>? OnDominoPlaced;
        public event Action<Player>? OnPlayerPassed;
        public event Action<
            Player?,                 // winner (null kalau tie)
            bool,                    // isBlocked
            IReadOnlyDictionary<Player, IReadOnlyList<Domino>>
        >? OnRoundEnded;
        public event Action<Player>? OnGameEnded;

        // ================= FIELDS =================
        private readonly List<Player> _players;
        private readonly Dictionary<Player, List<Domino>> _dominoInHands;
        private readonly IBoard _board;
        private Boneyard _boneyard;

        private int _currentPlayerIndex;
        // private int _roundLeaderIndex = -1;
        private int _consecutivePasses;
        private bool _roundEnded;
        private bool _isGameEnded;
        private Player? _gameWinner;
        private readonly int _maxScoreToWin;

        // ================= PROPERTIES =================
        public IReadOnlyList<Player> Players => _players;
        public Player CurrentPlayer => _players[_currentPlayerIndex];
        public IBoard Board => _board;
        public bool IsRoundEnded => _roundEnded;
        public bool IsGameEnded => _isGameEnded;
        public Player? GameWinner => _gameWinner;
        // public IEnumerable<Player> Players => _players;

        // ================= CONSTRUCTOR =================
        public GameController(List<Player> players, IBoard board, int maxScoreToWin)
        {
            _players = players;
            _board = board;
            _maxScoreToWin = maxScoreToWin;
            _dominoInHands = players.ToDictionary(p => p, _ => new List<Domino>());
            _boneyard = new Boneyard(GenerateFullSet());
        }

        // ================= GAME FLOW =================

        public void StartRound()
        {
            ResetRound();
            // DecideFirstPlayer();
            DealInitialHands();
            OnTurnChanged?.Invoke(CurrentPlayer);
        }

        public void NextTurn()
        {
            if (_roundEnded || _isGameEnded) return;

            var player = CurrentPlayer;

            if (!CanPlay(player))
            {
                PassTurn(player);
            }

            CheckRoundEnd();

            if (!_roundEnded)
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                OnTurnChanged?.Invoke(CurrentPlayer);
            }
        }
    
        // ================= PLAYER ACTIONS =================
        public IReadOnlyList<Domino> GetHands(Player player)
            => _dominoInHands[player].AsReadOnly();

        public bool PlayDomino(Player player, Domino domino, BoardSide side)
        {
            if (player != CurrentPlayer)
                return false;


            if (!_board.CanPlace(domino, side))
                return false;

            _board.Place(domino, side);
            _dominoInHands[player].Remove(domino);
            _consecutivePasses = 0;

            OnDominoPlaced?.Invoke(player, domino, side);
            
            return true;
        }

        public bool PassTurn(Player player)
        {
            if (player != CurrentPlayer)
                return false;

            _consecutivePasses++;
            OnPlayerPassed?.Invoke(player);
            
            return true;
        }

        public bool CanPlay(Player player)
        {
            if (_board.IsEmpty)
                return _dominoInHands[player].Count > 0;

            return _dominoInHands[player]
                .Any(d =>
                    _board.CanPlace(d, BoardSide.Left) ||
                    _board.CanPlace(d, BoardSide.Right));
        }


        public int CountPips(Player player)
        {
            return _dominoInHands[player]
                .Sum(d => (int)d.LeftPip + (int)d.RightPip);
        }

        // ================= ROUND END =================
        private void CheckRoundEnd()
        {
            // Normal win
            var emptyPlayer = _players.FirstOrDefault(p => _dominoInHands[p].Count == 0);
            
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

        private void HandleNormalWin(Player winner)
        {
            int score = _players
                .Where(p => p != winner)
                .SelectMany(p => _dominoInHands[p])
                .Sum(d => (int)d.LeftPip + (int)d.RightPip);

            winner.Score += score;
            OnRoundEnded?.Invoke(winner, false, SnapshotHands());
            CheckGameEnd();
        }

        private void HandleBlockedGame()
        {
            var pipTotals = _players.ToDictionary(p => p, CountPips);

            int min = pipTotals.Min(x => x.Value);
            var lowestPlayers = pipTotals.Where(x => x.Value == min).ToList();

            // Tie → no winner
            if (lowestPlayers.Count > 1)
            {
                OnRoundEnded?.Invoke(null, true, SnapshotHands());
                return;
            }

            var winner = lowestPlayers.First().Key;

            int gain = pipTotals.Sum(x => x.Value) - pipTotals[winner];
            winner.Score += gain;

            OnRoundEnded?.Invoke(winner, true, SnapshotHands());
            CheckGameEnd();
        }

        // ================= GAME END =================
        private void CheckGameEnd()
        {
            _gameWinner = _players.FirstOrDefault(p => p.Score >= _maxScoreToWin);
            if (_gameWinner != null)
            {
                _isGameEnded = true;
                OnGameEnded?.Invoke(_gameWinner);
            }
        }

        // ================= INTERNAL HELPERS =================
        private void ResetRound()
        {
            _board.Reset();
            _boneyard = new Boneyard(GenerateFullSet());
            _consecutivePasses = 0;
            _roundEnded = false;

            foreach (var p in _players)
                _dominoInHands[p].Clear();

        }

        private void DealInitialHands()
        {
            for (int i = 0; i < 7; i++)
                foreach (var p in _players)
                    _dominoInHands[p].Add(_boneyard.Draw());
        }
        private IEnumerable<Domino> GenerateFullSet()
        {
            for (int i = 0; i <= 6; i++)
                for (int j = i; j <= 6; j++)
                    yield return new Domino((Dot)i, (Dot)j);
        }

        private IReadOnlyDictionary<Player, IReadOnlyList<Domino>> SnapshotHands()
        {
            return _dominoInHands.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<Domino>)kvp.Value.AsReadOnly()
            );
        }

    }
}
