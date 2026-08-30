using System.Windows.Media;

namespace TinyQuakeLauncher.Models;

public class Difficulty
{
    public static Difficulty None =>
        new Difficulty
        {
            Name = "None",
            Value = -1,
            Foreground = System.Windows.Media.Brushes.Black
        };

    public string Name { get; set; } = "";

    public int Value { get; set; }

    public System.Windows.Media.Brush Foreground { get; set; } =
        System.Windows.Media.Brushes.Black;

    public override string ToString()
    {
        return Name;
    }
}