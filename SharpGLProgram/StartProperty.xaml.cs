﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System.ComponentModel;

namespace SharpGLProgram
{
    /// <summary>
    /// Interaction logic for call_1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        //call_1 LoadFile = new call_1();

        public Window1()
        {

            InitializeComponent();
            
        }

        public MainWindow call
        {
            get
            {
                throw new System.NotImplementedException();
            }
            set
            {
            }
        }

        void secondWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var secondWindow = sender as MainWindow;
            //string enteredText = secondWindow.EnteredText.Text;
        }

        private void StartLoadFromFile_Click(object sender, RoutedEventArgs e)
        {
            var secondWindow = new MainWindow();
            secondWindow.Closing += new CancelEventHandler(secondWindow_Closing);
            
            this.Close();
            secondWindow.Show();
            secondWindow.LoadFromFile(sender, e);
        }

        private void StartStream_Click(object sender, RoutedEventArgs e)
        {
            var secondWindow = new MainWindow();
            secondWindow.Closing += new CancelEventHandler(secondWindow_Closing);

            this.Close();
            secondWindow.Show();
            secondWindow.StreamButton_Click(sender, e);
        } 


    }
}
