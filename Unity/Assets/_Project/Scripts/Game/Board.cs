using GameOff.Data;
using UnityEngine;

namespace GameOff.Game
{
    public enum Dir { Right, Down, Left, Up }

    public enum CellKind { Floor, Wall, DoorClosed, DoorOpen, Arrow, Exit, CheckA, CheckB, Relay }

    public struct Cell
    {
        public CellKind Kind;
        public Dir Dir; // Arrow일 때만 의미 있음

        public bool Passable => Kind != CellKind.Wall && Kind != CellKind.DoorClosed;
    }

    /// <summary>LevelData 문자열을 두 층 격자로 해석한 결과.</summary>
    public sealed class Board
    {
        public int Width { get; }
        public int Height { get; }
        public Vector2Int Start { get; }
        public Cell[,] Reality { get; }
        public Cell[,] Blueprint { get; }

        public Board(LevelData level)
        {
            Height = level.realityRows.Length;
            Width = level.realityRows[0].Length;
            if (level.blueprintRows.Length != Height)
                Debug.LogError("[Board] 두 층의 행 수가 다릅니다.");

            Reality = new Cell[Width, Height];
            Blueprint = new Cell[Width, Height];
            Start = Vector2Int.zero;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    char r = CharAt(level.realityRows, x, y);
                    char b = CharAt(level.blueprintRows, x, y);
                    if (r == 'S') Start = new Vector2Int(x, y);
                    Reality[x, y] = Parse(r);
                    Blueprint[x, y] = Parse(b);
                }
            }
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        static char CharAt(string[] rows, int x, int y)
        {
            if (y >= rows.Length || x >= rows[y].Length)
            {
                Debug.LogError($"[Board] 격자 크기가 맞지 않습니다: ({x},{y})");
                return '#';
            }
            return rows[y][x];
        }

        static Cell Parse(char c)
        {
            switch (c)
            {
                case '#': return new Cell { Kind = CellKind.Wall };
                case 'D': return new Cell { Kind = CellKind.DoorClosed };
                case 'O': return new Cell { Kind = CellKind.DoorOpen };
                case '>': return new Cell { Kind = CellKind.Arrow, Dir = Dir.Right };
                case 'v': return new Cell { Kind = CellKind.Arrow, Dir = Dir.Down };
                case '<': return new Cell { Kind = CellKind.Arrow, Dir = Dir.Left };
                case '^': return new Cell { Kind = CellKind.Arrow, Dir = Dir.Up };
                case 'E': return new Cell { Kind = CellKind.Exit };
                case 'A': return new Cell { Kind = CellKind.CheckA };
                case 'B': return new Cell { Kind = CellKind.CheckB };
                case 'R': return new Cell { Kind = CellKind.Relay };
                case '.':
                case 'S': return new Cell { Kind = CellKind.Floor };
                default:
                    Debug.LogError($"[Board] 알 수 없는 문자 '{c}' — 바닥으로 처리합니다.");
                    return new Cell { Kind = CellKind.Floor };
            }
        }
    }
}
