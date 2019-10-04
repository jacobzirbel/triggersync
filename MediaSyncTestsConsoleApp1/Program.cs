using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//comment

namespace MediaSyncTestsConsoleApp1
{

    public class SplitParameters
    {
        private TimeSpan start;
        public SplitParameters(string inputFile, TimeSpan start, TimeSpan duration, string outputFile)
        {
            this.InputFile = inputFile;
            this.start = start;
            this.Duration = duration;
            this.OutputFile = outputFile;
        }
        public string InputFile { get; set; }
        public TimeSpan Start
        {
            get
            {
                if (this.start < new TimeSpan(0))
                {
                    return new TimeSpan(0);
                }
                else
                {
                    return this.start;
                }
            }
            set
            {
                this.start = value;
            }
        }
        public TimeSpan Duration { get; set; }
        public string OutputFile { get; set; }
    }

    public class Clip
    {
        public Trigger InitTrigger { get; set; }
        public string OutputName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<Footage> NecessaryFiles { get; set; }
        public SplitParameters SplitParameters { get; set; }
        public bool Within(DateTime dt)
        {
            if (dt < StartTime) return false;
            if (dt > EndTime) return false;
            return true;
        }

    }

    public class Footage
    {
        public DateTime CreateDate { get; set; }
        public double Duration { get; set; }
        public string FileName { get; set; }
        // Offset

        public DateTime EndDate { get { return CreateDate.AddMilliseconds((int)(Duration * 1000)); } }
        public bool Within(DateTime dt)
        {
            if (dt < CreateDate) return false;
            if (dt > EndDate) return false;
            return true;
        }

        public SplitParameters SplitParameters { get; set; }
    }

    public class Trigger
    {
        public DateTime CreateDate { get; set; }
        public DateTime ParamStart { get; set; }
        public DateTime ParamEnd { get; set; }
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

        static void Combine(List<string> inputs, string outputFile)
        {
            var endStuff = $@" -filter_complex ""[0:v] [0:a] [1:v] [1:a] concat=n={inputs.Count().ToString()}:v = 1:a = 1[v][a]"" -map ""[v]"" -map ""[a]"" {outputFile} ";
            var startStuff = "";
            foreach (var i in inputs)
            {
                startStuff += "-i " + i + " ";
            }
            var a2 = startStuff + endStuff;

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

        static void Combine(string firstFile, string secondFile, string outputFile)
        {
            //JZ get full path for files
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

        static string Split(SplitParameters pp)
        {
            var start = pp.Start.ToString();
            var inputFile = pp.InputFile;
            var duration = pp.Duration.ToString();
            var outputFile = pp.OutputFile + ".mp4";

            //https://stackoverflow.com/questions/45004159/ffmpeg-ss-and-t-for-cutting-mp3

            // -y command automatically overrides files
            var a2 = $@" -ss {start} -i ""{inputFile}"" -t {duration} -y ""{outputFile}""";
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

            return outputFile;
        }

        static void ProcessFolder(string folder)
        {
            // output each trigger and which footage files that contain it
            // output lists of footage folders in chronological order
            // output list of everything in chronological order

            var triggers = new List<Trigger>();
            var footageList = new Dictionary<string, IList<Footage>>();
            var clipList = new List<Clip>();
            var combineComps = new List<string>();

            foreach (var d in Directory.EnumerateDirectories(folder))
            {
                Console.WriteLine(d);
                if (d.Contains("iPhone") || d.Contains("Android"))
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
                    foreach (var f in (new DirectoryInfo(d)).EnumerateFiles("*.mp4"))
                    {
                        var v = GetInfo(f.FullName);
                        var s = v.streams?.Where(vv => vv.codec_type == "video").FirstOrDefault();
                        if (s != null)
                        {
                            var cd = s.Tags["creation_time"];
                            //
                            //
                            // Need to deal with this timezone thing
                            //
                            //
                            if((f.FullName.Contains("GoPr") || f.FullName.Contains("HERO") || f.FullName.Contains("GOP")) && !f.FullName.Contains("pixel"))
                            {
                                footage.Add(new Footage
                                {
                                    FileName = f.FullName,
                                    CreateDate = Convert.ToDateTime(cd).AddHours(5), // TODO TZ : I punted on timezone //
                                    Duration = s.duration
                                });
                            }
                            else
                            {
                                footage.Add(new Footage
                                {
                                    FileName = f.FullName,
                                    CreateDate = Convert.ToDateTime(cd).AddHours(0), // TODO TZ : I punted on timezone //
                                    Duration = s.duration
                                });
                            }
                            
                        }
                        else
                        {
                            Console.WriteLine($"INVALID : {f.FullName}");

                        }
                    }
                }
            }

            //each camera for a single trigger makes a "clip." clips can be made of multiple vids
            foreach (var trig in triggers)
            {          
                trig.ParamStart = trig.CreateDate - new TimeSpan(0, 0, before);
                trig.ParamEnd = trig.CreateDate + new TimeSpan(0, 0, after);

                foreach (var camera in footageList)
                {
                    var clip = new Clip
                    {
                        InitTrigger = trig,
                        OutputName = "output",
                        StartTime = trig.ParamStart,
                        EndTime = trig.ParamEnd,
                        NecessaryFiles = new List<Footage>()
                    }; 

                    foreach (var file in camera.Value)
                    {
                       if(file.Within(clip.StartTime) || file.Within(clip.EndTime))
                       {
                            clip.NecessaryFiles.Add(file);
                       }
                       else if(clip.Within(file.CreateDate))
                       {
                            clip.NecessaryFiles.Add(file);
                       }

                    }
                    if (clip.NecessaryFiles.Count > 0)
                    {
                        clipList.Add(clip);
                    }
                    
                }

            }

            //makes each clip from source footage, 2 files if necessary, doesn't work with 3+
            var clipNum = 0;
            foreach(var clip in clipList)
            {
                clipNum++;
                clip.SplitParameters = new SplitParameters(
                                                clip.NecessaryFiles.FirstOrDefault().FileName,
                                                ((clip.InitTrigger.CreateDate - clip.NecessaryFiles.FirstOrDefault().CreateDate) - tsBefore),
                                                tsBefore + tsAfter,
                                                "output" + clipNum.ToString());
               
                
                if(clip.NecessaryFiles.Count == 1)
                {
                    Split(clip.SplitParameters);
                }
                else if(clip.NecessaryFiles.Count == 0)
                {
                    //delete clip
                }
                else
                {
                    foreach(var file in clip.NecessaryFiles)
                    {
                        file.SplitParameters = new SplitParameters(
                            file.FileName,
                           ((clip.InitTrigger.CreateDate - file.CreateDate) - tsBefore),
                            tsBefore + tsAfter,
                            Path.GetFileName(file.FileName.TrimEnd(Path.DirectorySeparatorChar)) + "output"
                            );
                       
                        if (file.Within(clip.InitTrigger.ParamStart))
                        {
                            file.SplitParameters.Duration = file.EndDate - clip.InitTrigger.ParamStart;
                        }
                        if(file.Within(clip.InitTrigger.ParamEnd))
                        {
                            if (file.Within(clip.InitTrigger.ParamStart)) { /*error I don't think this should happen */ Console.WriteLine("fix error"); break; }
                            file.SplitParameters.Start = new TimeSpan(0);
                            file.SplitParameters.Duration = clip.InitTrigger.ParamEnd - file.CreateDate;
                        }

                        combineComps.Add(Split(file.SplitParameters));
                    }

                    Combine(combineComps, "combinedoutput.mp4");
                }
            }


            var sb = new StringBuilder();
            foreach (var f in footageList.First().Value.OrderBy(ff => ff.CreateDate))
            {
                sb.AppendLine($"{f.FileName} : {f.CreateDate} + {f.Duration} = {f.EndDate}");
            }
            foreach (var trig in triggers)
            {
                sb.AppendLine($"{trig.FileName} : {trig.CreateDate}");
                if (trig.Footage.Count > 0)
                {
                    foreach (var f in trig.Footage.First().Value.OrderBy(ff => ff.CreateDate))
                    {
                        sb.AppendLine($"{f.FileName} : {f.CreateDate} + {f.Duration} = {f.EndDate}");
                    }
                }
            }

            var footageListFileName = Path.Combine(folder, $"footage{DateTime.Now.Ticks}.txt");
            //File.WriteAllText(footageListFileName, sb.ToString());


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
        static public int before { get; set; }
        static public int after { get; set; }
        static public TimeSpan tsBefore { get; set; }
        static public TimeSpan tsAfter { get; set; }

        static void Main(string[] args)
        {

            string sourceFolder = args[0];
            int b, a;

            int.TryParse(args[1], out b);
            before = b;
            int.TryParse(args[2], out a);
            after = a;
            tsBefore = new TimeSpan(0, 0, b);
            tsAfter = new TimeSpan(0, 0, a);

            var footageFoldersList = new List<string>();

            foreach (var d in Directory.EnumerateDirectories(sourceFolder))
            {
                if (d.Contains("current"))
                {
                    footageFoldersList.Add(d);
                }
            }


            
            foreach (var v in footageFoldersList)
            {
                ProcessFolder(v);
            }

        }
    }
}



