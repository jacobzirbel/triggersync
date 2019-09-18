using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//git test

namespace MediaSyncTestsConsoleApp1
{
    public class Footage 
    {
        public DateTime CreateDate { get; set; }
        public double Duration { get; set; }
        public string FileName { get; set; }

        public DateTime EndDate { get { return CreateDate.AddMilliseconds((int)(Duration * 1000)); } }
        public bool Within(DateTime dt)
        {
            if (dt < CreateDate) return false;
            if (dt > EndDate) return false;
            return true;
        }
    }

    public class Trigger
    {
        public DateTime CreateDate { get; set; }
        public string FileName { get; set; }
        public IDictionary<string, IList<Footage>> Footage { get; set; }

        public Trigger()
        {
            Footage = new Dictionary<string, IList<Footage>>();
        }

        public void AddFootage(string footageSource, Footage footage)
        {
            if (!Footage.Keys.Contains(footageSource))
            {
                Footage.Add(footageSource, new List<Footage>());
            }
            Footage[footageSource].Add(footage);
        }

        public bool CheckAddFootage(string footageSource, Footage footage)
        {
            if (footage.Within(CreateDate))
            {
                AddFootage(footageSource, footage);
                return true;
            }
            return false;
        }
    }


    public class VidsStream
    {
        public int index { get; set; }
        public string codec_type { get; set; }
        public double duration { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }

    public class VidsProgram
    {

    }

    public class Vids
    {
        public IEnumerable<VidsProgram> programs { get; set; }
        public IEnumerable<VidsStream> streams { get; set; }
    }

    public static class Ext
    {
        public static void Serialize(object value, Stream s)
        {
            using (StreamWriter writer = new StreamWriter(s))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                JsonSerializer ser = new JsonSerializer();
                ser.Serialize(jsonWriter, value);
                jsonWriter.Flush();
            }
        }

        public static T Deserialize<T>(Stream s)
        {
            using (StreamReader reader = new StreamReader(s))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer ser = new JsonSerializer();
                return ser.Deserialize<T>(jsonReader);
            }
        }
    }
    class Program
    {
        const string ffmpegLocation = @"C:\Program Files (x86)\ffmpeg\bin";
        const string ffmpeg = ffmpegLocation + @"\ffmpeg.exe";
        const string ffprobe = ffmpegLocation + @"\ffprobe.exe";

        static void Combine(string firstFile, string secondFile, string outputFile)
        {
            // https://stackoverflow.com/questions/7333232/how-to-concatenate-two-mp4-files-using-ffmpeg
            var a2 = $@" -i ""{firstFile}"" -i ""{secondFile}"" -filter_complex ""[0:v] [0:a] [1:v] [1:a] concat=n=2:v=1:a=1 [v] [a]"" -map ""[v]"" -map ""[a]"" ""{outputFile}""";
            var psi = new ProcessStartInfo(ffmpeg, a2)
            {
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false
            };

            var p = new Process { StartInfo = psi };
            p.Start();

            while (!p.HasExited)
            {
                System.Threading.Thread.Sleep(100);
            }
        }

        static void Split(string inputFile, string start, string duration, string outputFile)
        {
            var a2 = $@" -ss {start} -i ""{inputFile}"" -t {duration} ""{outputFile}""";
            var psi = new ProcessStartInfo(ffmpeg, a2)
            {
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false
            };

            var p = new Process { StartInfo = psi };
            p.Start();

            while (!p.HasExited)
            {
                System.Threading.Thread.Sleep(100);
            }

        }

        static void ProcessFolder(string folder)
        {
            // output each trigger and which footage files that contain it
            // output lists of footage folders in chronological order
            // output list of everything in chronological order

            var triggers = new List<Trigger>();
            var footageList = new Dictionary<string, IList<Footage>>();

            foreach(var d in Directory.EnumerateDirectories(folder))
            {
                Console.WriteLine(d);
                if (d.Contains("iPhone"))
                {
                    // load triggers
                    foreach (var f in (new DirectoryInfo(d)).EnumerateFiles())
                    {
                        if (f.Extension.ToUpper() == ".JPG")
                        {
                            var pu = new RandomWayfarer.Pictures.PictureUtils();
                            var pic = pu.GetJpegPicture(f.FullName);
                            if (pic != null)
                            {
                                triggers.Add(new Trigger
                                {
                                    CreateDate = pic.DateTime,
                                    FileName = f.FullName
                                });
                            }
                            else
                            {
                                Console.WriteLine($"Invalid {f.FullName}");
                            }
                        }
                        else if (f.Extension.ToUpper() == ".MOV")
                        {
                            var v = GetInfo(f.FullName);
                            var s = v.streams.Where(vv => vv.codec_type == "video").First();
                            var cd = s.Tags["creation_time"];
                            triggers.Add(new Trigger
                            {
                                FileName = f.FullName,
                                CreateDate = Convert.ToDateTime(cd)
                            });
                        }
                    }
                }
                else
                {
                    var footage = new List<Footage>();
                    var ff = Path.GetFullPath(d);
                    var footageSourceName = ff.Split('\\').Reverse().First();
                    footageList.Add(footageSourceName, footage);
                    // load footage files
                    foreach (var f in (new DirectoryInfo(d)).EnumerateFiles())
                    {
                        var v = GetInfo(f.FullName);
                        var s = v.streams?.Where(vv => vv.codec_type == "video").FirstOrDefault();
                        if (s != null)
                        {
                            var cd = s.Tags["creation_time"];
                            footage.Add(new Footage
                            {
                                FileName = f.FullName,
                                CreateDate = Convert.ToDateTime(cd).AddHours(5), // TODO TZ : I punted on timezone
                                Duration = s.duration
                            });
                        }
                        else
                        {
                            Console.WriteLine($"INVALID : {f.FullName}");
                            
                        }
                    }
                }
            }

            foreach (var trig in triggers)
            {
                foreach (var foot in footageList)
                {
                    foreach (var x in foot.Value)
                    {
                        trig.CheckAddFootage(foot.Key, x);
                    }
                }
            }

            var sb = new StringBuilder();
            foreach(var f in footageList.First().Value.OrderBy(ff => ff.CreateDate))
            {
                sb.AppendLine($"{f.FileName} : {f.CreateDate} + {f.Duration} = {f.EndDate}");
            }
            foreach(var trig in triggers){
                sb.AppendLine($"{trig.FileName} : {trig.CreateDate}");
                if (trig.Footage.Count > 0)
                {
                    foreach (var f in trig.Footage.First().Value.OrderBy(ff => ff.CreateDate))
                    {
                        sb.AppendLine($"{f.FileName} : {f.CreateDate} + {f.Duration} = {f.EndDate}");
                    }
                }
            }


           foreach(var t in triggers)
           {
                ProcessTrigger(t, footageList);

                Console.WriteLine(t.CreateDate);
           }

            Split(@"C:\Users\Owner\Documents\Footage\HERO5 Black 1\GP031166.MP4", "0", "10", "output.mp4");

            var footageListFileName = Path.Combine(folder, $"footage{DateTime.Now.Ticks}.txt");
            File.WriteAllText(footageListFileName, sb.ToString());
        }

        static Vids GetInfo(string inputFile)
        {
            string a = $@" -v quiet ""{inputFile}"" -print_format json -show_entries stream=index,codec_type,duration:stream_tags=creation_time:format_tags=creation_time";
            var psi = new ProcessStartInfo(ffprobe, a)
            {
                RedirectStandardInput = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            var p = new Process { StartInfo = psi };
            p.Start();

            while (!p.HasExited)
            {
                System.Threading.Thread.Sleep(100);
            }

            var serializer = new JsonSerializer();

            using (var jsonTextReader = new JsonTextReader(p.StandardOutput))
            {
                var o = serializer.Deserialize<Vids>(jsonTextReader);
                return o;
            }
        }

        static void ProcessTrigger(Trigger t, Dictionary<string, IList<Footage>> allfootage)
        {
            foreach(var f in allfootage)
            {
                
            }

        }

        static void ProcessTriggerInFootage(Trigger t, IList<Footage> footagefolder)
        {

        }

        static void Main(string[] args)
        {//
            ProcessFolder(@"C:\Users\Owner\Documents\Footage");

            //foreach (var folder in Directory.EnumerateDirectories(@"E:\Footage"))
            //{
            //    ProcessFolder(folder);
            //}
            //ProcessFolder(@"E:\Footage\2018-09-10");
            // ProcessFolder(@"E:\Footage\2018-09-06");
            //ProcessFolder(@"C:\Users\tzerb\Documents\TestFootage\2018-09-08");

            //var outputFile = @"C:\Users\tzerb\Documents\TestFootage\Split.MP4";
            //var finalFile = @"C:\Users\tzerb\Documents\TestFootage\Final.MP4";
            //var inputFile = @"C:\Users\tzerb\Documents\TestFootage\2018-09-08\HERO5 Session 1\GOPR6628.MP4";
            //File.Delete(outputFile);
            //File.Delete(finalFile);
            //Split(inputFile, 10, 25, outputFile);
            //Combine(inputFile, outputFile, finalFile);

        }
    }
}
