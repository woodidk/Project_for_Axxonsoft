using Xunit;
using forAxxon.Models;
using Avalonia;

namespace forAxxon.Tests;

public class GeometryHelperTests
{
    [Fact]
    public void Distance_ShouldReturnCorrectValue()
    {

        var a = new Point(0, 0);
        var b = new Point(3, 4);

        var result = GeometryHelper.Distance(a, b);

        Assert.Equal(5.0, result, precision: 2);
    }

    [Fact]
    public void ComputeCentroid_ShouldReturnAveragePoint()
    {
        var points = new List<Point>
        {
            new Point(0, 0),
            new Point(2, 2)
        };

        var centroid = GeometryHelper.ComputeCentroid(points);

        Assert.Equal(1.0, centroid.X);
        Assert.Equal(1.0, centroid.Y);
    }

    [Fact]
    public void GetSquareFromDiagonal_ShouldReturn4Points()
    {
        var a = new Point(0, 0);
        var c = new Point(2, 2);

        var square = GeometryHelper.GetSquareFromDiagonal(a, c);

        Assert.NotNull(square);
        Assert.Equal(4, square.Count);
    }

    [Fact]
    public void IsPointInPolygon_WithTriangle_ShouldReturnTrueForInsidePoint()
    {
        var triangle = new List<Point>
        {
            new Point(0, 0),
            new Point(4, 0),
            new Point(2, 4)
        };
        var point = new Point(2, 1); 

        var isInside = GeometryHelper.IsPointInPolygon(point, triangle);

        Assert.True(isInside);
    }

    [Fact]
    public void IsPointInPolygon_WithTriangle_ShouldReturnFalseForOutsidePoint()
    {
        var triangle = new List<Point>
        {
            new Point(0, 0),
            new Point(4, 0),
            new Point(2, 4)
        };
        var point = new Point(5, 5);
        var isInside = GeometryHelper.IsPointInPolygon(point, triangle);

        Assert.False(isInside);
    }

    [Fact]
    public void ClampCircleOuterToNonNegativeArea_WhenRadiusTooLarge_ShouldClampToMaxAllowed()
    {
        var center = new Point(5, 5);
        var outer = new Point(15, 15); 
        var clamped = GeometryHelper.ClampCircleOuterToNonNegativeArea(center, outer);
        var radius = GeometryHelper.Distance(center, clamped);

        Assert.True(radius <= 5.0 + 1e-5); 
    }

    [Fact]
    public void ClampSquareOuterToNonNegativeArea_ShouldEnsureAllVerticesAreNonNegative()
    {
        var a = new Point(2, 2);
        var candidateC = new Point(-5, -5);

        var clampedC = GeometryHelper.ClampSquareOuterToNonNegativeArea(a, candidateC);
        var square = GeometryHelper.GetSquareFromDiagonal(a, clampedC);

        Assert.All(square, p => {
            Assert.True(p.X >= 0, "X must be non-negative");
            Assert.True(p.Y >= 0, "Y must be non-negative");
        });
    }
    [Fact]
    public void ToRuntimeModel_WithValidCircleDto_ShouldCreateCircle()
    {
        var dto = new SerializableShape { Type = "Circle", Points = [new(0, 0), new(1, 1)] };
        var shape = ShapeConverter.ToRuntimeModel(dto);
        Assert.IsType<Circle>(shape);
    }
    [Fact]
    public void StartCircleCommand_ShouldSetCurrentShapeToCircle()
    {
        var mockDialogService = new MockDialogService(); 
        var vm = new MainWindowViewModel(mockDialogService);
        vm.StartCircleCommand.Execute(null);
        Assert.IsType<Circle>(vm.CurrentShape);
    }
}