using System.Windows.Media;

namespace TinyQuakeLauncher.Models;

public class Difficulty
{
    public string Name { get; set; } = "";

    public int Value { get; set; }

    public System.Windows.Media.Brush Foreground { get; set; } =
        System.Windows.Media.Brushes.Black;

    public override string ToString()
    {
        return Name;
    }
}