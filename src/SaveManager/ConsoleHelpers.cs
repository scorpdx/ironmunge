using System;

namespace SaveManager
{
    public static class ConsoleHelpers
    {
        private static ReadOnlySpan<char> ParseColorFormat(ReadOnlySpan<char> formattedText, out Action write)
        {

            var controlIndex = formattedText.IndexOf('%');
            if (controlIndex == -1)
            {
                var fullString = formattedText.ToString();
                write = () => Console.Write(fullString);
                return null;
            }
            controlIndex++;

            //  ""; -1, length 0
            // "%";  0, length 1
            //"%%";  0, length 2
            if (controlIndex + 1 >= formattedText.Length)
            {
                throw new InvalidOperationException("Format must be specified after control character");
            }

            //escaped
            var str = formattedText.Slice(0, controlIndex - 1).ToString();
            if (formattedText[controlIndex] == '%')
            {
                //include the '%'
                write = () => Console.Write(str);
                return formattedText.Slice(controlIndex + 1);
            }

            write = () => Console.Write(str);

            //control
            bool foreground = false;
            bool background = false;
            switch (formattedText[controlIndex])
            {
                case 'f': foreground = true; break;
                case 'b': background = true; break;
                case 'B':
                    foreground = true;
                    background = true;
                    break;
                case 'R':
                    write += () => Console.ResetColor();
                    return formattedText.Slice(controlIndex + 1);
                default:
                    throw new InvalidOperationException("Unrecognized control sequence");
            }

            var controlText = formattedText.Slice(controlIndex + 1);
            if (controlText.IsEmpty)
                throw new InvalidOperationException("Unfinished control sequence");

            ConsoleColor color;
            switch (controlText[0])
            {
                case 'k': color = ConsoleColor.Black; break;
                case 'b': color = ConsoleColor.Blue; break;
                case 'B': color = ConsoleColor.DarkBlue; break;
                case 'G': color = ConsoleColor.DarkGreen; break;
                case 'C': color = ConsoleColor.DarkCyan; break;
                case 'R': color = ConsoleColor.DarkRed; break;
                case 'M': color = ConsoleColor.DarkMagenta; break;
                case 'Y': color = ConsoleColor.DarkYellow; break;
                case 'a': color = ConsoleColor.Gray; break;
                case 'g': color = ConsoleColor.Green; break;
                case 'c': color = ConsoleColor.Cyan; break;
                case 'r': color = ConsoleColor.Red; break;
                case 'm': color = ConsoleColor.Magenta; break;
                case 'y': color = ConsoleColor.Yellow; break;
                case 'w': color = ConsoleColor.White; break;
                default: throw new InvalidOperationException("Unrecognized color sequence");
            }

            if (foreground)
                write += () => Console.ForegroundColor = color;
            if (background)
                write += () => Console.BackgroundColor = color;

            return controlText.Slice(1);
        }

        public static void ConsoleWriteColored(ReadOnlySpan<char> formattedText)
        {
            Action? completeWrite = null;
            do
            {
                formattedText = ParseColorFormat(formattedText, out Action write);
                completeWrite += write;
            } while (formattedText != null);
            completeWrite?.Invoke();
        }
    }
}
