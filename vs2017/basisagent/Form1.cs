using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Text.RegularExpressions;
using Dart.Telnet;
using System.IO;
using System.Collections.Specialized;
using System.Configuration;
using System.Threading;

namespace basisagent
{
    public partial class Form1 : Form
    {

        static string telnet_text = "";
        static Telnet telnet1 = new Telnet();
        static TelnetModel telnetModel = new TelnetModel();


        public static basisagentEntities db = new basisagentEntities();


        private ContextMenuStrip strip = new ContextMenuStrip();//1



        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;

            list_init();


            // run thread per 10 second
            Control.CheckForIllegalCrossThreadCalls = false;
            Thread m1Thread = new Thread(new ThreadStart(m1));
            m1Thread.IsBackground = true;
            m1Thread.Start();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //Wire-up events
            telnet1.Data += new EventHandler<DataEventArgs>(telnet1_Data);
            telnet1.Error += new EventHandler<Dart.Telnet.ErrorEventArgs>(telnet1_Error);

            //log stream event
            listView_rulehost.MouseClick += new MouseEventHandler(listView3MouseClick);

            strip.Items.Add("add", null, new EventHandler(MenuItem_Click_add));
            // strip.Items.Add("delete", null, new EventHandler(MenuItem_Click_delete));

            worklog("Program start...");
        }


        private void listView3MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                toolStripStatusLabel1.Text = this.listView_rulehost.SelectedItems[0].SubItems[0].Text;
                strip.Show(listView_rulehost, e.Location);//listview3 鼠标右键按下弹出菜单
            }
        }


        //导入文件
        private void MenuItem_Click_add(object sender, System.EventArgs e)
        {

            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Multiselect = true;
            fileDialog.Title = "Please select files";
            fileDialog.Filter = "files(*.*)|*.*";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                string[] names = fileDialog.FileNames;

                foreach (string file in names)
                {
                    MessageBox.Show("Selected file:" + file, "Select file tip", MessageBoxButtons.OK, MessageBoxIcon.Information);



                    var lines = File.ReadAllLines(file);
                    string[] arr;
                    foreach (var line in lines)
                    {
                        arr = line.Split(' ');

                        //write DB
                        rule_aix_df one = new rule_aix_df();
                        one.hostname = arr[0];
                        one.strcommand = arr[1];
                        one.strhead = arr[2];
                        one.strtail = arr[3];

                        db.rule_aix_df.Add(one);



                    }


                }

            }


            db.SaveChanges();

        }





        //////////////////////// 
        /////// telnet   /////// 
        //////////////////////// 
        void telnet1_logon(string hostname)
        {
            telnetModel = new TelnetModel();
            telnetModel.Telnet = telnet1;

            try
            {

                //找到
                var q = (from obj in db.host_command where obj.hostname.Equals(hostname) select obj).FirstOrDefault();

                telnetModel.Session.RemoteEndPoint.HostNameOrAddress = q.strip;
                telnetModel.Session.RemoteEndPoint.Port = 23;
                telnetModel.Credentials.Username = q.strusername;
                telnetModel.Credentials.Password = q.strpassword;
                telnetModel.Credentials.CommandPrompt = q.strcommandpromat;
                telnetModel.CommandString = q.strcommand;

                telnetModel.Execute(null);
            }
            catch (Exception ex)
            {
                worklog(ex.Message);
               
            }
        }

        void telnet1_Data(object sender, DataEventArgs e)
        {
            telnet_text = telnet_text + Encoding.ASCII.GetString(e.Data.Buffer, e.Data.Offset, e.Data.Count);
        }

        void telnet1_Error(object sender, Dart.Telnet.ErrorEventArgs e)
        {
            string expText = e.GetException().ToString();

            worklog(expText);

        }


        ////////////////////////////// 
        /////// telnet string  /////// 
        ////////////////////////////// 

        private List<string> str_line(string hostname, string bigtxt, string head, string end)
        {
            //按首位关键字，提取中间字符串内容
            string tmp = str_ka(bigtxt, head, end);

            //把一窜文本，按空格分割字符
            string[] str_array = Regex.Split(tmp, "\\s+", RegexOptions.IgnoreCase);

            //过滤空格，得到5个字段的数据
            List<string> list = new List<string>();

            list.Add(hostname); //加上hostname字段
            list.Add(head); //加上lv字段
            foreach (string s in str_array)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    list.Add(s);
                }
            }
            list.Add(end);//加上mount点字段

            return list;
        }

        // s = a1bcdefga1     begin = a1b   end = a1 : return = cdefg 
        private string str_ka(string s, string begin, string end)
        {
            s = s.Substring(s.IndexOf(begin) + begin.Length); //找开始的位置,把头去掉  
            s = s.Substring(0, s.IndexOf(end));//找结束的位置,把尾巴去掉
            return s;
        }









        public void list_init()
        {



            //list1
            listView_handstream.View = View.Details;
            listView_handstream.GridLines = true;
            listView_handstream.Clear();
            listView_handstream.Columns.Add("hostname        ");
            listView_handstream.Columns.Add("Filesystem                ");
            listView_handstream.Columns.Add("GB blocks          ");
            listView_handstream.Columns.Add("Free     ");
            listView_handstream.Columns.Add("%Used   ");
            listView_handstream.Columns.Add("Iused   ");
            listView_handstream.Columns.Add("%Iused    ");
            listView_handstream.Columns.Add("Mounted on              ");
            listView_handstream.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);


            //list2
            listView_host.View = View.Details;
            listView_host.GridLines = true;
            listView_host.Clear();
            listView_host.Columns.Add("hostname      ");
            listView_host.Columns.Add("describe      ");        
            listView_host.Columns.Add("ip         ");
            listView_host.Columns.Add("adapter     ");
            listView_host.Columns.Add("  OS    ");
            listView_host.Columns.Add("username     ");
            listView_host.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);


            foreach (host_command one in db.host_command)
            {
                //listview控件显示
                ListViewItem item = new ListViewItem();
                listView_host.Items.Add(item);
                item.Text = one.hostname;
                item.SubItems.Add(one.hosttext);
                item.SubItems.Add(one.strip);                
                item.SubItems.Add(one.adapter);
                item.SubItems.Add(one.os);
                item.SubItems.Add(one.strusername);

            }


            //_rulehost
            listView_rulehost.View = View.Details;
            listView_rulehost.GridLines = true;
            listView_rulehost.Clear();
            listView_rulehost.Columns.Add("hostname      ");

            listView_rulehost.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

            //找到
            var q = (from obj in db.rule_aix_df select obj.hostname).Distinct();


            foreach (string one in q)
            {
                //listview控件显示
                ListViewItem item = new ListViewItem();
                listView_rulehost.Items.Add(item);
                item.Text = one;


            }



            //_rulelib
            listView_rulelib.View = View.Details;
            listView_rulelib.GridLines = true;
            listView_rulelib.Clear();
            listView_rulelib.Columns.Add("hostname      ");
            listView_rulelib.Columns.Add("str command ");
            listView_rulelib.Columns.Add("str head                  ");
            listView_rulelib.Columns.Add("str tail     ");
            listView_rulelib.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

            ////////////////////////////////////////////
            //hostname to combobox
            //找到
            var h = (from obj in db.log_aix_df select obj.hostname).Distinct();

            foreach (string one in h)
            {
                comboBox1.Items.Add(one);
                
            }


            //////////// logstream
            listView_logstream.View = View.Details;
            listView_logstream.GridLines = true;
            listView_logstream.Clear();
            listView_logstream.Columns.Add("hostname        ");
            listView_logstream.Columns.Add("Filesystem                ");
            listView_logstream.Columns.Add("GB blocks          ");
            listView_logstream.Columns.Add("Free     ");
            listView_logstream.Columns.Add("%Used   ");
            listView_logstream.Columns.Add("Mounted on              ");
            listView_logstream.Columns.Add("    ");
            listView_logstream.Columns.Add("time point      ");
            listView_logstream.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);


            //////////// schedule time ////////////

            listView_time.View = View.Details;
            listView_time.GridLines = true;
            listView_time.Clear();
            listView_time.Columns.Add("Reminder        ");

            listView_time.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

            foreach (string key in ConfigurationManager.AppSettings.AllKeys)
            {
                string value = ConfigurationManager.AppSettings[key];              

             
                    //listview控件显示
                    ListViewItem item = new ListViewItem();
                    listView_time.Items.Add(item);
                    item.Text = value;         


            }



            listView_worklog.View = View.Details;
            listView_worklog.GridLines = true;
            listView_worklog.Clear();
            listView_worklog.Columns.Add("Datetime             ");
            listView_worklog.Columns.Add("text                                              ");    
            listView_worklog.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);


            listView_Alarm.View = View.Details;
            listView_Alarm.GridLines = true;
            listView_Alarm.Clear();
            listView_Alarm.Columns.Add("Datetime             ");
            listView_Alarm.Columns.Add("text                                              ");
            listView_Alarm.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);





            //init over
        }


        private void worklog(string s)
        {
            DateTime thistime = DateTime.Now;

            //listview控件显示
            ListViewItem item = new ListViewItem();
            listView_worklog.Items.Add(item);
            item.Text = thistime.ToString("g");
            item.SubItems.Add(s);

        }




        private void listView_rulehost_DoubleClick(object sender, EventArgs e)
        {
            if (this.listView_rulehost.SelectedItems.Count == 0) return;

            telnet_text = "";
            listView_rulelib.Items.Clear();

            //fdjeccprd1
            string hostone = listView_rulehost.FocusedItem.Text;

            //找到这个主机的规则
            var q = from obj in db.rule_aix_df where obj.hostname.Equals(hostone) select obj;

            foreach (rule_aix_df s in q)
            {
                //listview控件显示
                ListViewItem item = new ListViewItem();
                listView_rulelib.Items.Add(item);
                item.Text = s.hostname;
                item.SubItems.Add(s.strcommand);
                item.SubItems.Add(s.strhead);
                item.SubItems.Add(s.strtail);

            }
        }


        ////////////////////////
        ////// monitor    //////
        ////////////////////////

        private void listView_host_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                if (this.listView_host.SelectedItems.Count == 0) return;

                telnet_text = "";
                listView_handstream.Items.Clear();

                //fdjeccprd1
                string hostone = listView_host.FocusedItem.Text;

                //  MessageBox.Show(hostone);

                telnet_text = "";  //attenion , must clear previous telnet text
                telnet1_logon(hostone);
                //   textBox1.Text = telnet_text;

                DateTime thistime = DateTime.Now;

                //找到这个主机的规则
                var q = from obj in db.rule_aix_df where obj.hostname.Equals(hostone) select obj;


                foreach (rule_aix_df s in q)
                {
                    List<string> list = str_line(s.hostname, telnet_text, s.strhead, s.strtail);


                    

                   
                    //listview控件显示
                    ListViewItem item = new ListViewItem();
                    listView_handstream.Items.Add(item);
                    item.Text = list[0];
                    item.SubItems.Add(list[1]);
                    item.SubItems.Add(list[2]);
                    item.SubItems.Add(list[3]); 
                    item.SubItems.Add(list[4]); 
                    item.SubItems.Add(list[5]);
                    item.SubItems.Add(list[6]);
                    item.SubItems.Add(list[7]);
                   
                    //write DB
                    log_aix_df one = new log_aix_df();
                    one.savetime = thistime;
                    one.hostname = list[0];
                    one.filesystem = list[1];
                    one.GBblocks = list[2];
                    one.diskfree = list[3];
                    one.diskused = list[4];
                    one.iused = list[5];
                    one.iusedpercent = list[6];
                    one.mounted = list[7];

                    //check Alarm
                    string temp = list[4].Replace("%", "");
                    if (i(temp) > 90)
                    {
                        item.ForeColor = Color.Red;
                        one.alarm_checked = "alarm";
                    }


                    db.log_aix_df.Add(one);
                 
                }

                db.SaveChanges();

            }


            catch (Exception ex)
            {
                worklog(ex.Message);

            }


        }

        
        ////////////////////////
        ////// log stream //////
        ////////////////////////
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

            comboBox2.Items.Clear();
            string s = comboBox1.SelectedItem.ToString();

            var alltime = (from a in db.log_aix_df where a.hostname == s select new { a.savetime }).Distinct();

            foreach (var t in alltime)
            {
                comboBox2.Items.Insert(0,t.savetime.ToString());

            }
        }
        
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

            string s1 = comboBox1.SelectedItem.ToString(); //hostname
            var one = (from c in db.log_aix_df where c.hostname.Equals(s1) select new { c.id }).FirstOrDefault(); //主机ID

            string s = comboBox2.SelectedItem.ToString(); //时间

            DateTime ts = t(s);

            var allpage = from a in db.log_aix_df
                          where
                                  (
                                //  a.id == one.id &&
                                  a.savetime.Value.Month == ts.Month &&
                                  a.savetime.Value.Year == ts.Year &&
                                  a.savetime.Value.Day == ts.Day &&
                                  a.savetime.Value.Hour == ts.Hour &&
                                  a.savetime.Value.Minute == ts.Minute &&
                                  a.savetime.Value.Second == ts.Second
                                 )
                          select a;

            listView_logstream.BeginUpdate();//工作线程用这个不会闪烁  
            listView_logstream.Items.Clear();
         
            foreach (var line in allpage)
            {
                ListViewItem item = new ListViewItem();//每读到一行就创建一项    
                listView_logstream.Items.Add(item);//然后加到Listview  
                item.Text = line.hostname.ToString();
                item.SubItems.Add(line.filesystem.ToString());
                item.SubItems.Add(line.GBblocks.ToString());
                item.SubItems.Add(line.diskfree.ToString());
                item.SubItems.Add(line.diskused.ToString()); string temp = line.diskused.Replace("%", ""); if (i(temp) > 90) { item.ForeColor = Color.Red; }
                item.SubItems.Add(line.mounted.ToString());
                item.SubItems.Add("");
                item.SubItems.Add(line.savetime.ToString());

              
            }

            listView_logstream.EndUpdate();
          //  toolStripStatusLabel5.Text = "Total Size：" + (alltotal / 1000).ToString() + "G"; 
            
        }


        /////////////////////////////
        ////// schedule worklog/////
        ////////////////////////////

        private void m1()
        {
            while (true)
            {
                Thread.Sleep(10 * 1000);

                string now = System.DateTime.Now.ToShortTimeString();     //get now time point 


                if (listView_host.Items.Count == 0) return;
               
                 foreach (ListViewItem lt in listView_time.Items)
                    {

                        if (now.Equals(lt.SubItems[0].Text.ToString()))   //time point 1:12
                        {
                            auto_monitor();
                            load_alarm(); //finish once monitor , then load alarm
                            Thread.Sleep(1000 * 60);//延迟一分钟，避免一分钟内发多次
                        }
                    }
                
            }
        }






        private void auto_monitor()
        {
            try
            {
               

                var allhost = from obj in db.host_command where ( obj.adapter.Equals("telnet") && obj.os.Equals("aix") && obj.strcommand.Equals("df -g") )  select obj;
                
                foreach (host_command onehost in allhost)
                {

                    telnet_text = "";  //attenion , must clear previous telnet text
                    telnet1_logon(onehost.hostname); //telnet hostname
                    Thread.Sleep(1000 * 10);//延迟10秒

                    DateTime thistime = DateTime.Now;

                    //找到这个主机的规则
                    var q = from obj in db.rule_aix_df where obj.hostname.Equals(onehost.hostname) select obj;

                    foreach (rule_aix_df s in q)
                    {
                        List<string> list = str_line(s.hostname, telnet_text, s.strhead, s.strtail);

                        //write DB
                        log_aix_df one = new log_aix_df();
                        one.savetime = thistime;
                        one.hostname = list[0];
                        one.filesystem = list[1];
                        one.GBblocks = list[2];
                        one.diskfree = list[3];
                        one.diskused = list[4];
                        one.iused = list[5];
                        one.iusedpercent = list[6];
                        one.mounted = list[7];


                        //check Alarm
                        string temp = list[4].Replace("%", "");
                        if (i(temp) > 90)
                        {                            
                            one.alarm_checked = "alarm";
                        }


                        db.log_aix_df.Add(one);
                       
                    }

                    worklog(onehost.hostname + " info db saved.");
               
                }
                db.SaveChanges();

            }


            catch (Exception ex)
            {
                worklog(ex.Message);

            }


        }




        ////////////////////////
        ////// Alarm mail //////
        ////////////////////////



           
        private void clear_alarm()
        {
            try
            {
                listView_Alarm.Items.Clear();
             
                var q = from obj in db.log_aix_df where obj.alarm_checked.Equals("alarm") select obj;


                foreach (log_aix_df line in q)
                {           
                        line.alarm_checked = "clear";                  
                }                  

                db.SaveChanges();
            }


            catch (Exception ex)
            {
                worklog(ex.Message);

            }


        }

  


        private void load_alarm()
        {
            try
            {
                listView_Alarm.Items.Clear();

                //找到这个主机的规则
                var q = from obj in db.log_aix_df where obj.alarm_checked.Equals("alarm") select obj;
                
                foreach (log_aix_df line in q)
                {

                        ListViewItem item = new ListViewItem();//每读到一行就创建一项    
                        listView_Alarm.Items.Add(item);//然后加到Listview  
                        item.Text =  line.savetime.ToString();
                        item.SubItems.Add(line.hostname + "  " + line.filesystem + " " + line.diskused + " " + line.mounted);
                      
                }
            }

            catch (Exception ex)
            {
                worklog(ex.Message);

            }
        }







        ////////////////////////
        ////// Tool ///////////
        ////////////////////////

        public static int i(object x1)
        {
            if (string.IsNullOrWhiteSpace(x1.ToString())) { return 0; }
            return Convert.ToInt32(x1);

        }

        public static DateTime t(object x1)
        {

            if (s(x1).Equals("0000-00-00")) { x1 = null; }
            return Convert.ToDateTime(x1);

        }

        public static string s(object x1)
        {
            return Convert.ToString(x1);

        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            
            System.Diagnostics.Process.Start("https://blog.csdn.net/ot512csdn/article/details/80352073");
            
        }

       

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            load_alarm();
        }

        private void toolStripButton5_Click(object sender, EventArgs e)
        {
            clear_alarm();
        }
    }
}
