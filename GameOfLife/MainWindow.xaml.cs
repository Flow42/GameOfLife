using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// TODO Create a stop Button
namespace GameOfLife
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
		Game g = new Game { Height = 50, Width = 50, ScaleX = 8, ScaleY = 8, Epochs=20, StopGameTokenSource = new CancellationTokenSource()};
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = g;
        }

        /// <summary>
        /// Applies the logical not of the GameState entry, that is implied by the clicked position
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlipGameCell(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(g.GameVisual);
            // rescale coordinates to fit the bound bool array
            int x = (int)(pos.X / g.ScaleY);
            int y = (int)(pos.Y / g.ScaleX);
            g.GameState[y, x] = !g.GameState[y, x];
            g.RaisePropertyChanged("GameState");
        }

        private void CreateBmp(object sender, RoutedEventArgs e)
        {
            
            if(!(GameViewBox.Child is null))
            {
                g.StopGameTokenSource.Cancel();
            }
            g.InitGameState();
            Image img = new Image();
            GameViewBox.Child = img;
            img.Margin = new Thickness() { Left = 5, Right = 5, Top = 5, Bottom = 5 };
            img.Stretch = Stretch.UniformToFill;
            img.StretchDirection = StretchDirection.DownOnly;
            img.MouseLeftButtonDown += FlipGameCell;
            Binding binding = new Binding("GameState");
            binding.Converter = new ArrayBitmapSourceConverter() { ScaleX = g.ScaleX, ScaleY = g.ScaleY };
            img.SetBinding(Image.SourceProperty, binding);
            g.GameVisual = img;
        }

        private async void RunGame(object sender, RoutedEventArgs e)
        {
            CancellationToken token = g.StopGameTokenSource.Token;
            for (int i = 0; i < g.Epochs; i++)
            {
                await Task.Delay(1000);
                if (!token.IsCancellationRequested)
                {
                    // adjust for zero based counting
                    g.CurrentEpoch = i + 1;
                    g.RaisePropertyChanged("CurrentEpoch");
                    g.MakeTurn();
                }
                else
                {
                    // reset the cancellationtoken, so the function runs again
                    g.StopGameTokenSource.Dispose();
                    g.StopGameTokenSource = new CancellationTokenSource();
                    // reset the currentEpoch value and report it to GUI
                    g.CurrentEpoch = 0;
                    g.RaisePropertyChanged("CurrentEpoch");
                    return;
                }
            }
            MessageBox.Show("Finished");
        }
    }
    /// <summary>
    /// A Converter class to be used in a Data Binding.
    /// Converts a 2d boolean array to a BitmapSource and vice versa
    /// </summary>
    public class ArrayBitmapSourceConverter : IValueConverter
    {
        // use properties to mirror the corresponding values from the Game class
        public int ScaleX { get; set; }
        public int ScaleY { get; set; }

        private bool[,] RectScale(bool[,] array, bool scaleUp = true)
        {
            int scaleX = ScaleX;
            int scaleY = ScaleY;
            int height = array.GetLength(0);
            int width = array.GetLength(1);
            if (scaleUp)
            {
                int newHeight = height * scaleX;
                int newWidth = width * scaleY;
                bool[,] scaledArray = new bool[newHeight, newWidth];
                for (int i = 0; i < newHeight; i += scaleX)
                {
                    for (int j = 0; j < newWidth; j += scaleY)
                    {
                        for (int x = 0; x < scaleX; x++)
                        {
                            for (int y = 0; y < scaleY; y++)
                            {
                                scaledArray[i + x, j + y] = array[i / scaleX, j / scaleY];
                            }
                        }
                    }
                }
                return scaledArray;
            }
            else
            {
                int newHeight = height / scaleX;
                int newWidth = width / scaleY;
                bool[,] scaledArray = new bool[newHeight, newWidth];
                for(int i = 0; i < newHeight; i++)
                {
                    for(int j = 0; j < newWidth; j++)
                    {
                        // the given array has boxes of size ScaleX*ScaleY, with every pixel
                        // holding the same value, so use the upper right pixel value for the downscaled array
                        scaledArray[i, j] = array[i * scaleX, j * scaleY];
                    }
                }
                return scaledArray;
            }
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.GetType() == typeof(bool[,]))
            {
                bool[,] array = (bool[,])value;
                array = RectScale(array);
                int height = array.GetLength(0);
                int width = array.GetLength(1);
                byte[] pixelData = new byte[height * width];
                int cnt = 0;
                foreach (bool b in array)
                {
                    if (b) { pixelData[cnt] = 255; }
                    else { pixelData[cnt] = 0; }
                    cnt++;
                }
                PixelFormat pf = PixelFormats.Gray8;
                int stride = width;
                BitmapSource bmpSource = BitmapSource.Create(width, height, 96, 96, pf, BitmapPalettes.BlackAndWhite, pixelData, stride);
                return bmpSource;
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value.GetType() == typeof(BitmapSource))
            {
                BitmapSource bmpSrc = (BitmapSource)value;
                int height = bmpSrc.PixelHeight;
                int width = bmpSrc.PixelWidth;
                PixelFormat pf = bmpSrc.Format;
                // stride formula is: (width * pf.BitsPerPixel + 7) / 8. 
                // Since the Gray8 Pixelformat uses one byte per pixel it simplifies to:
                int stride = width;
                Int32Rect rect = new Int32Rect(0, 0, width, height);
                byte[,] pixelData = new byte[height, width];
                bmpSrc.CopyPixels(pixelData, stride, 0);
                bool[,] array = new bool[height, width];
                for(int i = 0; i < height; i++)
                {
                    for(int j = 0; j < width; j++)
                    {
                        if(pixelData[i,j] != 0)
                        {
                            array[i, j] = true;
                        }
                    }
                }
                return RectScale(array, false);
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Logic for computing Conway's Game of Life rules and viewing them using Bitmaps
    /// </summary>
    public class Game : INotifyPropertyChanged
    {
        // TODO implement status box (showing what the program currently does)
        public int Height { get; set; }
        public int Width { get; set; }
        public int ScaleX { get; set; }
        public int ScaleY { get; set; }
        public int Epochs { get; set; }
        public int CurrentEpoch { get; set; }
        public bool[,] GameState { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public Image GameVisual { get; set; }
        public CancellationTokenSource StopGameTokenSource { get; set; }
        
        public void GameCancel()
        {
            StopGameTokenSource.Cancel(true);
        }

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        // TODO implement means of initialization by adding default patterns (glider etc.)
        public void InitGameState()
        {
            this.GameState = new bool[this.Height, this.Width];
            Random rand = new Random();
            int cntx = 0;
            int cnty = 0;
            for (int i=0;i<Math.Min(this.Height, this.Width); ++i)
            {
                this.GameState[cntx, cnty] = true;
                cntx++;
                cnty++;
            }
            //for(int n = 0; n < this.Height; ++n)
            //{
            //    for(int m = 0; m < this.Width; ++m)
            //    {
            //        if (rand.NextDouble() >= 0.5)
            //        {
            //            this.GameState[n, m] = true;
            //        }
            //    }
            //}
        }
        /// <summary>
        /// Applies the Conways Game of Life's rules to a given boolean array (one game iteration).
        /// </summary>
        /// <param name="cells"> A boolean array representing the cells, where true means alive.</param>
        /// <returns></returns>
        public void MakeTurn()
        {
            bool[,] cells = this.GameState;
            int m = cells.GetLength(0);
            int n = cells.GetLength(1);
            int cnt = 0;
            // values are initially set to false, meaning false values needn't be set explicitly
            bool[,] result = new bool[m, n];
            bool[,] padded = new bool[m + 2, n + 2];
            // pad the given array with a one pixel border
            for(int i = 0; i < m; i++)
            {
                for(int j = 0; j < n; j++)
                {
                    padded[i + 1, j + 1] = cells[i, j];
                }
            }
            // apply the rules on the padded array, shifting all indices in the padded array by +1 while iterating over result
            for (int i=0; i<m; i++)
            {
                for (int j=0; j<n; j++)
                {
                    // above
                    if (padded[i, j+1]) {cnt++; }
                    // left above
                    if (padded[i, j]) { cnt++; }
                    // left
                    if (padded[i+1, j]) {cnt++; }
                    // left below
                    if (padded[i + 2, j]) { cnt++; }
                    // below
                    if (padded[i + 2, j+1]) { cnt++; }
                    // right below
                    if (padded[i + 2, j + 2]) { cnt++; }
                    // right
                    if (padded[i+1, j + 2]) { cnt++; }
                    // right above
                    if (padded[i, j+2]) {cnt++; }
                    // a live cell with 2 or 3 neighbors lives on
                    if (padded[i+1,j+1] && cnt==2 || cnt == 3) { result[i, j] = true; }
                    // a dead cell with 3 neighbors lives in the next step
                    if (!padded[i+1,j+1] && cnt==3) { result[i, j] = true; }
                    // reset counter variable
                    cnt = 0;
                }
            }
            this.GameState = result;
            RaisePropertyChanged("GameState");
        }
    }
}
