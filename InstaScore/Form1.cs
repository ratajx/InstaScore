using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Diagnostics;

namespace InstaScore
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private String connectionString = "workstation id=InstaScoreDB.mssql.somee.com;packet size=4096;user id=LaionikInsta_SQLLogin_1;pwd=instascore;data source=InstaScoreDB.mssql.somee.com;persist security info=False;initial catalog=InstaScoreDB";
        private String[,] tabPhoto;
        private int actualIndex, tabSize;
        private static List<String> urlList,userList;

        public static long dateToUnixTimestamp(DateTime target)
        {
            var date = new DateTime(1970, 1, 1, 0, 0, 0, target.Kind);
            var unixTimestamp = System.Convert.ToInt64((target - date).TotalSeconds);

            return unixTimestamp;
        }

        public static DateTime unixTimestampToDate(String unixTime)
        {
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(Double.Parse(unixTime)).ToLocalTime();

            return dtDateTime;
        }

        public void downloadPhoto(String url)
        {
            try{
            HttpWebRequest httpWebRequest = (HttpWebRequest)HttpWebRequest.Create(url);
            HttpWebResponse httpWebReponse = (HttpWebResponse)httpWebRequest.GetResponse();
            Stream stream = httpWebReponse.GetResponseStream();
            Bitmap bitmap = new Bitmap(stream);
            pictureBox1.Image = bitmap;
            }
            catch (WebException ex)
            {
                MessageBox.Show(ex.Message);
                //actualIndex++;
            }
        }

        public void updatePhotoInDatabase(String Id, String visbleValue)
        {
            string update = @"
                update photos
                set photoVisible='" + visbleValue + "' where photoID = '" + Id + "'";
            using (SqlConnection thisConnection = new SqlConnection(connectionString))
            {
                using (SqlCommand query = new SqlCommand(update, thisConnection))
                {
                    thisConnection.Open();
                    query.ExecuteNonQuery();
                }
            }
        }

        public void getPhotoStatusFromDatabase(int index)
        {
            if (tabPhoto[index, 1] == "1")
            {
                label5.Text = "widoczne";
                label5.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                label5.Text = "ukryte";
                label5.ForeColor = System.Drawing.Color.Red;
            }
        }

        public void downloadPhotosDatabase()
        {
            string sql1 = "SELECT COUNT (*) FROM photos";
            string sql2 = "SELECT * FROM photos";
            SqlDataReader rdr = null;
            int i = 0;
            tabSize = 0;
            tabPhoto = null;
            using (SqlConnection thisConnection = new SqlConnection(connectionString))
            {
                using (SqlCommand cmdCount = new SqlCommand(sql1, thisConnection))
                {
                    thisConnection.Open();
                    tabSize = (int)cmdCount.ExecuteScalar();
                    cmdCount.CommandText = sql2;
                    rdr = cmdCount.ExecuteReader();
                    tabPhoto = new String[tabSize, 3];
                    while (rdr.Read())
                    {
                        tabPhoto[i, 0] = (String)rdr["photoURL"];
                        if ((bool)rdr["photoVisible"])
                            tabPhoto[i, 1] = "1";
                        else
                            tabPhoto[i, 1] = "0";
                        tabPhoto[i, 2] = Convert.ToString(rdr["photoID"]);
                        i++;
                    }
                    thisConnection.Close();
                    if (tabSize > 0)
                    {
                        downloadPhoto(tabPhoto[0, 0]);
                        getPhotoStatusFromDatabase(0);
                        actualIndex = 0;
                        label11.Text = tabSize.ToString();
                        label13.Text = (actualIndex + 1).ToString();
                    }
                    else
                        MessageBox.Show("Baza jest pusta!");
                }
            }
        }

        public void insertPotosToDatabase()
        {
            using (SqlConnection thisConnection = new SqlConnection(connectionString))
            {
                thisConnection.Open();
                for (int i = 0; i < urlList.Count; i++)
                {
                    string stmt = @"
                    insert into photos
                    (photoURL, photoScore, photoTotal, photoVisible,photoProfile)
                    values ('" + urlList[i] + "','0','0','1','https://instagram.com/" + userList[i] + "')";
                    using (SqlCommand query = new SqlCommand(stmt, thisConnection))
                    {

                        query.ExecuteNonQuery();
                    }
                }
                thisConnection.Close();
            }
        }

        public static JObject downloadPhotoFromInstagram(String next_url, String tag, int limitLikes)
        {
            WebClient client = new WebClient();
            var data = "";
            if (next_url == "")
            {
                data = client.DownloadString("https://api.instagram.com/v1/tags/" + tag + "/media/recent?access_token=236552726.1fb234f.82be3ad0138841edbe68b3b67a504116");
            }
            else
            {
                data = client.DownloadString(next_url);
            }
            JObject jsonObject = JObject.Parse(data);

            for (int i = 0; i < 20; i++)
            {
                if ((int)jsonObject["data"][i]["likes"]["count"] > limitLikes)
                {
                    String url = jsonObject["data"][i]["images"]["standard_resolution"]["url"].ToString();

                    String userName = jsonObject["data"][i]["user"]["username"].ToString();

                    urlList.Add(url);
                    userList.Add(userName);
                }
            }
            return jsonObject;
        }

        public void downloadAllPhotosOnInstagram()
        {
            long date, selectedDate = dateToUnixTimestamp(dateTimePicker1.Value) - 7200;

            int i = 0;
            String nextUrl = "";
            urlList = new List<string>();
            userList = new List<string>();
            JObject jsonObject=null;

            Stopwatch watch = new Stopwatch();
            try
            {
                watch.Start();
                do
                {

                  jsonObject = downloadPhotoFromInstagram(nextUrl, textBox2.Text, Convert.ToInt32(textBox3.Text));

                    System.DateTime dtDateTime = unixTimestampToDate(jsonObject["data"][0]["created_time"].ToString());
                    date = Int32.Parse(jsonObject["data"][0]["created_time"].ToString());
                    nextUrl = jsonObject["pagination"]["next_url"].ToString();

                    // Console.WriteLine(i);
                    Console.WriteLine(dtDateTime);
                    // Console.WriteLine(date);

                    i++;
                    if (textBox1.Text != "")
                    {
                        if (urlList.Count > Convert.ToInt32(textBox1.Text))
                            date = 0;
                        else
                            Console.WriteLine(urlList.Count);
                    }
                    else
                        Console.WriteLine(urlList.Count);

                    if (i == 4999)
                    {
                        watch.Stop();
                        int time = (1000 * 3600) - (int)watch.ElapsedMilliseconds;
                        if(time>0)
                            Thread.Sleep(time);
                        watch.Reset();
                        watch.Start();
                        i = 0;
                    }
                    
                } while (date > selectedDate);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                StreamWriter sw = new StreamWriter("log.txt");
                if(jsonObject!=null)
                    sw.Write(jsonObject["pagination"]["next_url"].ToString());
                sw.Close();
            }
        }

        public void deleteAll()
        {
            string stmt = @"
            delete from photos
            where photoID > '0'";
            using (SqlConnection thisConnection = new SqlConnection(connectionString))
            {
                using (SqlCommand query = new SqlCommand(stmt, thisConnection))
                {
                    thisConnection.Open();
                    query.ExecuteNonQuery();
                }
            }
        }

        public String searchNextUrl(long SelectedDate, String tag)
        {
            WebClient client = new WebClient();
            JObject jsonObject;
            String nextUrl = "";
            long date;
            var data = "";
            client.Proxy = null;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            do
            {
                if (nextUrl == "")
                    data = client.DownloadString("https://api.instagram.com/v1/tags/" + tag + "/media/recent?access_token=236552726.1fb234f.82be3ad0138841edbe68b3b67a504116");
                else
                    data = client.DownloadString(nextUrl);

                jsonObject = JObject.Parse(data);
                nextUrl = jsonObject["pagination"]["next_url"].ToString();
                date = date = Int32.Parse(jsonObject["data"][19]["created_time"].ToString());
                //Console.WriteLine(date);
            } while (date > SelectedDate);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);
            return nextUrl;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox2.Text != "")
            {
                if (textBox3.Text != "")
                {
                    if (dateToUnixTimestamp(dateTimePicker1.Value) < dateToUnixTimestamp(DateTime.Now))
                    {
                        try
                        {
                           downloadAllPhotosOnInstagram();
                           insertPotosToDatabase();
                          // label11.Text = urlList.Count.ToString();
                           //tabPhoto = new String[urlList.Count, 2];
                           //for (int i = 0; i < urlList.Count; i++)
                           //    tabPhoto[i, 0] = urlList[i];
                           //downloadPhoto(tabPhoto[0, 0]);
                           //getPhotoStatusFromDatabase(0);
                           //actualIndex = 0;
                           //tabSize = urlList.Count;
                           //label11.Text = tabSize.ToString();
                           //label13.Text = (actualIndex + 1).ToString();

                           button2.Enabled = true;
                           button3.Enabled = true;
                           button4.Enabled = true;
                           button5.Enabled = true;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                    else
                        MessageBox.Show("Wybrana ddata nie może wskazywać przyszłości!");
                }
                else
                    MessageBox.Show("Wpisz limit like-ów!");
            }
            else
                MessageBox.Show("Wpisz tag!");
        }

        private void button6_Click(object sender, EventArgs e)
        {
            downloadPhotosDatabase();
            button2.Enabled = true;
            button3.Enabled = true;
            button4.Enabled = true;
            button5.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if ((actualIndex + 1) < tabSize)
            {
                downloadPhoto(tabPhoto[actualIndex + 1, 0]);
                getPhotoStatusFromDatabase(actualIndex + 1);
                actualIndex++;
                label13.Text = (actualIndex + 1).ToString();
            }
            else
                MessageBox.Show("To już ostatnie zdjęcie!");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (actualIndex > 0)
            {
                downloadPhoto(tabPhoto[actualIndex - 1, 0]);
                getPhotoStatusFromDatabase(actualIndex - 1);
                actualIndex--;
                label13.Text = (actualIndex + 1).ToString();
            }
            else
                MessageBox.Show("To pierwsze zdjęcie z bazy!");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (tabPhoto[actualIndex, 1] != "1")
            {
                tabPhoto[actualIndex, 1] = "1";
                getPhotoStatusFromDatabase(actualIndex);
                updatePhotoInDatabase(tabPhoto[actualIndex, 2], "1");
            }

        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (tabPhoto[actualIndex, 1] != "0")
            {
                tabPhoto[actualIndex, 1] = "0";
                getPhotoStatusFromDatabase(actualIndex);
                updatePhotoInDatabase(tabPhoto[actualIndex, 2], "0");
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Jesteś pewien że chcesz wyczyścić baze?", "Uwaga",
       MessageBoxButtons.OKCancel);
            switch (result)
            {
                case DialogResult.OK:
                    {
                        DialogResult result2 = MessageBox.Show("Na 100%?", "Uwaga!!!!!!!!!",
                        MessageBoxButtons.OKCancel);
                        switch (result2)
                        {
                            case DialogResult.OK:
                                {
                                    deleteAll();
                                    break;
                                }
                            case DialogResult.Cancel:
                                {
                                    break;
                                }
                        }
                        break;
                    }
                case DialogResult.Cancel:
                    {
                        break;
                    }
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (urlList.Count != 0)
                insertPotosToDatabase();
            else
                MessageBox.Show("Lista zdjęć jest pusta!");
        }
    }
}