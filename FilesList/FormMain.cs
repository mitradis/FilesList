using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace FilesList
{
    public partial class FormMain : Form
    {
        List<string> outList = new List<string>();
        string lastPath = null;
        string reader = null;
        int pathLength = 0;

        public FormMain()
        {
            InitializeComponent();
            bool found = false;
            RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            if (baseKey != null)
            {
                RegistryKey regkey = baseKey.OpenSubKey("SOFTWARE\\7-Zip");
                if (regkey != null)
                {
                    string path = (string)regkey.GetValue("Path");
                    if (path != null && File.Exists(Path.Combine(path, "7z.exe")))
                    {
                        reader = Path.Combine(path, "7z.exe");
                        found = true;
                    }
                    regkey.Dispose();
                }
                baseKey.Dispose();
            }
            if (!found)
            {
                ClientSize = new System.Drawing.Size(318, 51);
                button2.Visible = false;
            }
        }

        void button1_Click(object sender, EventArgs e)
        {
            enableDisable(false);
            if (lastPath != null)
            {
                folderBrowserDialog1.SelectedPath = lastPath;
            }
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                lastPath = folderBrowserDialog1.SelectedPath;
                pathLength = pathAddSlash(lastPath).Length;
                searchFolder(lastPath);
                writeFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), checkFolder()));
            }
            enableDisable(true);
        }

        void button2_Click(object sender, EventArgs e)
        {
            enableDisable(false);
            if (lastPath != null)
            {
                openFileDialog1.InitialDirectory = lastPath;
            }
            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                lastPath = Path.GetDirectoryName(openFileDialog1.FileNames[0]);
                bool multi = openFileDialog1.FileNames.Length > 1;
                foreach (string line in openFileDialog1.FileNames)
                {
                    parseArchive(line, multi);
                }
            }
            enableDisable(true);
        }

        void parseArchive(string file, bool multi)
        {
            string[] output = null;
            try
            {
                Process process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(866);
                process.StartInfo.FileName = reader;
                process.StartInfo.Arguments = "l -slt \"" + file + "\"";
                process.Start();
                output = process.StandardOutput.ReadToEnd().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                process.WaitForExit();
            }
            catch
            {
                MessageBox.Show("Не удалось запустить: " + reader + " va \"" + file + "\"");
            }
            if (output != null)
            {
                string path = null;
                string size = null;
                string modified = null;
                string crc = null;
                bool folder = false;
                bool start = false;
                bool end = false;
                int count = output.Length;
                for (int i = 0; i < count; i++)
                {
                    if (!start && output[i].StartsWith("----------"))
                    {
                        start = true;
                    }
                    else if (start)
                    {
                        end = i + 1 == count;
                        if (!String.IsNullOrEmpty(output[i]) || end)
                        {
                            if (end || output[i].StartsWith("Path = "))
                            {
                                if (!String.IsNullOrEmpty(path) && !String.IsNullOrEmpty(size) && !String.IsNullOrEmpty(modified) && !String.IsNullOrEmpty(crc))
                                {
                                    if (folder)
                                    {
                                        if (checkBox4.Checked)
                                        {
                                            outList.Add(path + (checkBox1.Checked ? "\t" + modified : ""));
                                        }
                                    }
                                    else
                                    {
                                        outList.Add(path + (checkBox2.Checked ? "\t" + crc : "") + (checkBox1.Checked ? "\t" + size + "\t" + modified : ""));
                                    }
                                }
                                path = null;
                                size = null;
                                modified = null;
                                crc = null;
                                folder = false;
                                if (!end)
                                {
                                    path = output[i].Remove(0, 7);
                                }
                            }
                            else if (output[i].StartsWith("Folder = "))
                            {
                                folder = output[i].Remove(0, 9) == "+";
                            }
                            else if (output[i].StartsWith("Size = "))
                            {
                                size = output[i].Remove(0, 7);
                            }
                            else if (output[i].StartsWith("Modified = "))
                            {
                                modified = output[i].Remove(0, 11);
                                modified = modified.Remove(modified.IndexOf('.'));
                                DateTime parse;
                                if (DateTime.TryParse(modified, out parse))
                                {
                                    modified = parse.ToString();
                                }
                            }
                            else if (output[i].StartsWith("CRC = "))
                            {
                                crc = output[i].Remove(0, 6);
                            }
                        }
                    }
                }
                string dir = null;
                if (multi)
                {
                    dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), checkFolder());
                    if (!Directory.Exists(dir))
                    {
                        try
                        {
                            Directory.CreateDirectory(dir);
                        }
                        catch
                        {
                            MessageBox.Show("Не удалось создрать папку: " + dir);
                        }
                    }
                }
                writeFile(multi ? Path.Combine(dir, Path.GetFileNameWithoutExtension(file)) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Path.GetFileNameWithoutExtension(file)));
            }
        }

        string checkFolder()
        {
            DirectoryInfo info = new DirectoryInfo(lastPath);
            return info.Parent != null ? info.Name : info.Name.Remove(1);
        }

        void searchFolder(string path)
        {
            getFilesList(path);
            foreach (string line in getDirectories(path))
            {
                if (Directory.Exists(line) && (new DirectoryInfo(line).Attributes & FileAttributes.System) != FileAttributes.System)
                {
                    if (checkBox4.Checked)
                    {
                        try
                        {
                            outList.Add(line.Remove(0, pathLength) + (checkBox1.Checked ? "\t" + Directory.GetLastWriteTime(line) : ""));
                        }
                        catch
                        {
                            outList.Add(line.Remove(0, pathLength) + "\tNO ACCESS TO FOLDER");
                        }
                    }
                    searchFolder(line);
                }
            }
        }

        void getFilesList(string path)
        {
            foreach (string line in getFiles(path))
            {
                if (!File.GetAttributes(line).HasFlag(FileAttributes.System))
                {
                    try
                    {
                        FileInfo info = new FileInfo(line);
                        outList.Add(line.Remove(0, pathLength) + (checkBox2.Checked ? "\t" + getCRC(line) : "") + (checkBox1.Checked ? "\t" + info.Length + "\t" + info.LastWriteTime : "") + (checkBox3.Checked && info.IsReadOnly ? "\tREAD-ONLY" : ""));
                    }
                    catch
                    {
                        outList.Add(line.Remove(0, pathLength) + "\tNO ACCESS TO FILE");
                    }
                }
            }
        }

        string[] getFiles(string path)
        {
            try
            {
                return Directory.GetFiles(path);
            }
            catch
            {
                return new string[] { };
            }
        }

        string[] getDirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path);
            }
            catch
            {
                return new string[] { };
            }
        }

        string getCRC(string file)
        {
            string line = "";
            FileStream fs = File.OpenRead(file);
            line = String.Format("{0:X}", calculateCRC(fs));
            fs.Close();
            while (line.Length < 8)
            {
                line = "0" + line;
            }
            return line;
        }

        uint calculateCRC(Stream stream)
        {
            const int buffer_size = 1024;
            const uint POLYNOMIAL = 0xEDB88320;
            uint result = 0xFFFFFFFF;
            uint Crc32;
            byte[] buffer = new byte[buffer_size];
            uint[] table = new uint[256];
            unchecked
            {
                for (int i = 0; i < 256; i++)
                {
                    Crc32 = (uint)i;
                    for (int j = 8; j > 0; j--)
                    {
                        if ((Crc32 & 1) == 1)
                        {
                            Crc32 = (Crc32 >> 1) ^ POLYNOMIAL;
                        }
                        else
                        {
                            Crc32 >>= 1;
                        }
                    }
                    table[i] = Crc32;
                }
                int count = stream.Read(buffer, 0, buffer_size);
                while (count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        result = ((result) >> 8) ^ table[(buffer[i]) ^ ((result) & 0x000000FF)];
                    }
                    count = stream.Read(buffer, 0, buffer_size);
                }
            }
            buffer = null;
            table = null;
            stream.Close();
            return ~result;
        }

        void writeFile(string file)
        {
            if (File.Exists(file + ".txt"))
            {
                int i = 1;
                while (true)
                {
                    if (File.Exists(file + " (" + i.ToString() + ")" + ".txt"))
                    {
                        i++;
                    }
                    else
                    {
                        file = file + " (" + i.ToString() + ")" + ".txt";
                        break;
                    }
                }
            }
            else
            {
                file = file + ".txt";
            }
            try
            {
                outList.Sort();
                File.WriteAllLines(file, outList);
            }
            catch
            {
                MessageBox.Show("Не удалось записать файл: " + file);
            }
            outList.Clear();
        }

        void enableDisable(bool enable)
        {
            button1.Text = enable ? "Путь" : "Работает";
            button1.Enabled = enable;
            button2.Enabled = enable;
            checkBox1.Enabled = enable;
            checkBox2.Enabled = enable;
            checkBox3.Enabled = enable;
            checkBox4.Enabled = enable;
        }

        string pathAddSlash(string path)
        {
            if (!path.EndsWith("/") && !path.EndsWith(@"\"))
            {
                if (path.Contains("/"))
                {
                    path += "/";
                }
                else if (path.Contains(@"\"))
                {
                    path += @"\";
                }
            }
            return path;
        }
    }
}
