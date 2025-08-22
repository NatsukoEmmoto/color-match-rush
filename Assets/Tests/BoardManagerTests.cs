using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ColorMatchRush;

public class BoardManagerTests
{
    private BoardManager boardManager;
    private GameObject boardGameObject;

    [SetUp]
    public void SetUp()
    {
        // Create a GameObject with BoardManager for testing
        boardGameObject = new GameObject("TestBoardManager");
        boardManager = boardGameObject.AddComponent<BoardManager>();
        
        // Set up a minimal configuration for testing
        SetPrivateField(boardManager, "width", 5);
        SetPrivateField(boardManager, "height", 5);
        SetPrivateField(boardManager, "cellSize", 1f);
        
        // Initialize the grid with null values for manual setup
        var grid = new Piece[5, 5];
        SetPrivateField(boardManager, "grid", grid);
    }

    [TearDown]
    public void TearDown()
    {
        if (boardGameObject != null)
        {
            Object.DestroyImmediate(boardGameObject);
        }
    }

    [Test]
    public void FindAllMatches_EmptyGrid_ReturnsEmptySet()
    {
        // Test with completely empty grid
        var matches = boardManager.FindAllMatches();
        
        Assert.IsNotNull(matches);
        Assert.AreEqual(0, matches.Count);
    }

    [Test]
    public void FindAllMatches_HorizontalMatch_ReturnsCorrectPositions()
    {
        // Create test pieces for horizontal match
        var grid = GetGrid(boardManager);
        
        // Create three red pieces in a row: (0,0), (0,1), (0,2)
        grid[0, 0] = CreateTestPiece(0, 0, Piece.PieceType.Red);
        grid[0, 1] = CreateTestPiece(0, 1, Piece.PieceType.Red);
        grid[0, 2] = CreateTestPiece(0, 2, Piece.PieceType.Red);
        
        var matches = boardManager.FindAllMatches();
        
        Assert.AreEqual(3, matches.Count);
        Assert.IsTrue(matches.Contains(new Vector2Int(0, 0))); // col=0, row=0
        Assert.IsTrue(matches.Contains(new Vector2Int(1, 0))); // col=1, row=0
        Assert.IsTrue(matches.Contains(new Vector2Int(2, 0))); // col=2, row=0
    }

    [Test]
    public void FindAllMatches_VerticalMatch_ReturnsCorrectPositions()
    {
        // Create test pieces for vertical match
        var grid = GetGrid(boardManager);
        
        // Create three blue pieces in a column: (0,0), (1,0), (2,0)
        grid[0, 0] = CreateTestPiece(0, 0, Piece.PieceType.Blue);
        grid[1, 0] = CreateTestPiece(1, 0, Piece.PieceType.Blue);
        grid[2, 0] = CreateTestPiece(2, 0, Piece.PieceType.Blue);
        
        var matches = boardManager.FindAllMatches();
        
        Assert.AreEqual(3, matches.Count);
        Assert.IsTrue(matches.Contains(new Vector2Int(0, 0))); // col=0, row=0
        Assert.IsTrue(matches.Contains(new Vector2Int(0, 1))); // col=0, row=1
        Assert.IsTrue(matches.Contains(new Vector2Int(0, 2))); // col=0, row=2
    }

    [Test]
    public void FindAllMatches_BothHorizontalAndVertical_ReturnsAllPositions()
    {
        var grid = GetGrid(boardManager);
        
        // Create horizontal match: (1,1), (1,2), (1,3)
        grid[1, 1] = CreateTestPiece(1, 1, Piece.PieceType.Green);
        grid[1, 2] = CreateTestPiece(1, 2, Piece.PieceType.Green);
        grid[1, 3] = CreateTestPiece(1, 3, Piece.PieceType.Green);
        
        // Create vertical match: (2,0), (3,0), (4,0)
        grid[2, 0] = CreateTestPiece(2, 0, Piece.PieceType.Yellow);
        grid[3, 0] = CreateTestPiece(3, 0, Piece.PieceType.Yellow);
        grid[4, 0] = CreateTestPiece(4, 0, Piece.PieceType.Yellow);
        
        var matches = boardManager.FindAllMatches();
        
        Assert.AreEqual(6, matches.Count);
        
        // Check horizontal match
        Assert.IsTrue(matches.Contains(new Vector2Int(1, 1)));
        Assert.IsTrue(matches.Contains(new Vector2Int(2, 1)));
        Assert.IsTrue(matches.Contains(new Vector2Int(3, 1)));
        
        // Check vertical match
        Assert.IsTrue(matches.Contains(new Vector2Int(0, 2)));
        Assert.IsTrue(matches.Contains(new Vector2Int(0, 3)));
        Assert.IsTrue(matches.Contains(new Vector2Int(0, 4)));
    }

    [Test]
    public void FindAllMatches_WithNullPieces_SkipsNullsCorrectly()
    {
        var grid = GetGrid(boardManager);
        
        // Create match with null in between - should not match
        grid[0, 0] = CreateTestPiece(0, 0, Piece.PieceType.Red);
        grid[0, 1] = null; // null piece
        grid[0, 2] = CreateTestPiece(0, 2, Piece.PieceType.Red);
        
        // Create valid vertical match
        grid[1, 3] = CreateTestPiece(1, 3, Piece.PieceType.Purple);
        grid[2, 3] = CreateTestPiece(2, 3, Piece.PieceType.Purple);
        grid[3, 3] = CreateTestPiece(3, 3, Piece.PieceType.Purple);
        
        var matches = boardManager.FindAllMatches();
        
        // Should only find the vertical match with Purple pieces
        Assert.AreEqual(3, matches.Count);
        Assert.IsTrue(matches.Contains(new Vector2Int(3, 1))); // col=3, row=1
        Assert.IsTrue(matches.Contains(new Vector2Int(3, 2))); // col=3, row=2
        Assert.IsTrue(matches.Contains(new Vector2Int(3, 3))); // col=3, row=3
    }

    [Test]
    public void FindAllMatches_NoMatches_ReturnsEmptySet()
    {
        var grid = GetGrid(boardManager);
        
        // Create a checkerboard pattern with no matches
        grid[0, 0] = CreateTestPiece(0, 0, Piece.PieceType.Red);
        grid[0, 1] = CreateTestPiece(0, 1, Piece.PieceType.Blue);
        grid[1, 0] = CreateTestPiece(1, 0, Piece.PieceType.Blue);
        grid[1, 1] = CreateTestPiece(1, 1, Piece.PieceType.Red);
        
        var matches = boardManager.FindAllMatches();
        
        Assert.AreEqual(0, matches.Count);
    }

    [Test]
    public void FindAllMatches_LongerSequence_ReturnsAllPositions()
    {
        var grid = GetGrid(boardManager);
        
        // Create a 4-piece horizontal match
        for (int col = 0; col < 4; col++)
        {
            grid[2, col] = CreateTestPiece(2, col, Piece.PieceType.Green);
        }
        
        var matches = boardManager.FindAllMatches();
        
        Assert.AreEqual(4, matches.Count);
        for (int col = 0; col < 4; col++)
        {
            Assert.IsTrue(matches.Contains(new Vector2Int(col, 2)));
        }
    }

    // Helper methods
    private Piece CreateTestPiece(int row, int col, Piece.PieceType type)
    {
        var pieceGameObject = new GameObject($"TestPiece_{row}_{col}");
        var piece = pieceGameObject.AddComponent<Piece>();
        
        // Initialize the piece using its public method
        piece.Initialize(row, col, type, Vector3.zero);
        
        return piece;
    }

    private Piece[,] GetGrid(BoardManager manager)
    {
        return (Piece[,])GetPrivateField(manager, "grid");
    }

    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(obj, value);
    }

    private object GetPrivateField(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(obj);
    }
}