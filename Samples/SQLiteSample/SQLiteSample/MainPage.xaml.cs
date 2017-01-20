using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Import Microsoft.Data.Sqlite namespaces
using Microsoft.Data.Sqlite;
using Microsoft.Data.Sqlite.Internal; // Not technically necessary, only needed for SqliteEngine.UseWinSqlite3() call.

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SQLiteSample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            Output.ItemsSource = Grab_Entries();
        }

        // Method to insert text into the SQLite database
        private void Add_Text(object sender, RoutedEventArgs e)
        {
            using (SqliteConnection db = new SqliteConnection("Filename=sqliteSample.db"))
            {
                db.Open();
                SqliteCommand insertCommand = new SqliteCommand("INSERT INTO MyTable VALUES (NULL, '" + Input_Box.Text + "');", db);
                try
                {
                    insertCommand.ExecuteReader();
                }
                catch (SqliteException error)
                {
                    //Handle error
                    return;
                }
                db.Close();
            }
            Output.ItemsSource = Grab_Entries();
        }

        // Method to grab Text_Entry column from MyTable table in SQLite database
        private List<String> Grab_Entries()
        {
            List<String> entries = new List<string>();
            using (SqliteConnection db = new SqliteConnection("Filename=sqliteSample.db"))
            {
                db.Open();
                SqliteCommand selectCommand = new SqliteCommand("SELECT Text_Entry from MyTable", db);
                SqliteDataReader query;
                try
                {
                    query = selectCommand.ExecuteReader();
                }
                catch (SqliteException error)
                {
                    //Handle error
                    return entries;
                }
                while (query.Read())
                {
                    entries.Add(query.GetString(0));
                }
                db.Close();
            }
            return entries;
        }
    }
}
