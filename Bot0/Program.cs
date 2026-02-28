
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

/**
 * Auto-generated code below aims at helping you parse
 * the standard input according to the problem statement.
 **/
class Player
{
    static void Main(string[] args)
    {

        // game loop
        while (true)
        {
            for (int i = 0; i < 8; i++)
            {
                string[] inputs = Console.ReadLine().Split(' ');
                int colorA = int.Parse(inputs[0]); // color of the first block
                int colorB = int.Parse(inputs[1]); // color of the attached block
            }
            int score1 = int.Parse(Console.ReadLine());
            for (int i = 0; i < 12; i++)
            {
                string row = Console.ReadLine(); // One line of the map ('.' = empty, '0' = skull block, '1' to '5' = colored block)
            }
            int score2 = int.Parse(Console.ReadLine());
            for (int i = 0; i < 12; i++)
            {
                string row = Console.ReadLine();
            }

            // Write an action using Console.WriteLine()
            // To debug: Console.Error.WriteLine("Debug messages...");


            // "x rotation": the column in which to drop your pair of blocks followed by its rotation (0, 1, 2 or 3)
            Console.WriteLine("0 1");
        }
    }
}
