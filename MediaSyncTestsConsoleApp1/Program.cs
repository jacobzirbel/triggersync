using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MediaSyncTestsConsoleApp1
{

    public class SplitParameters
    {
        public string InputFile { get; set; }
        public TimeSpan Start { get; set; }
        public string duration { get; set; }
        public string OutputFile { get; set; }
    }

    public class Clip
    {
        public Trigger InitTrigger { get; set; }
        public string OutputName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Duration { get; set; }
        public List<Footage> NecessaryFiles { get; set; }
        public SplitParameters SplitParameters { get; set; }
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

        static void Split(SplitParameters pp)
        {
            var start = pp.Start.ToString();
            var inputFile = pp.InputFile;
            var duration = pp.duration;
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
            

        }

        static void ProcessFolder(string folder, List<string> outputList)
        {
            // output each trigger and which footage files that contain it
            // output lists of footage folders in chronological order
            // output list of everything in chronological order

            var triggers = new List<Trigger>();
            var footageList = new Dictionary<string, IList<Footage>>();
            var clipList = new List<Clip>();

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
                            // question improve this
                            if(f.FullName.Contains("GOPR"))
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


            foreach (var trig in triggers)
            {
                
                //trig.CreateDate += new TimeSpan(0, 0, 6);//because of android silliness. needed for "androidcase1" folder
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
                        Duration = before + after,
                        NecessaryFiles = new List<Footage>()
                    }; 

                    foreach (var file in camera.Value)
                    {
                       if(file.Within(clip.StartTime) || file.Within(clip.EndTime))
                       {
                            clip.NecessaryFiles.Add(file);
                       }

                    }
                    clipList.Add(clip);
                }

            }

            foreach(var clip in clipList)
            {
                clip.SplitParameters = new SplitParameters
                {
                    InputFile = clip.NecessaryFiles.First().FileName,
                    Start = ((clip.InitTrigger.CreateDate - clip.NecessaryFiles.First().CreateDate) - tsBefore),
                    duration = (before + after).ToString(),
                    OutputFile = "output"
                };
                
                if(clip.NecessaryFiles.Count == 1)
                {
                    //question can this be part of the class?
                    if(clip.SplitParameters.Start < new TimeSpan(0))
                    {
                        clip.SplitParameters.Start = new TimeSpan(0);
                    }
                    
                    Split(clip.SplitParameters);
                }
                else
                {
                    foreach(var file in clip.NecessaryFiles)
                    {
                       // var necessaryInputs = RunSplit(file)
                       // Combine(necessaryInputs)
                    }
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


            outputList = RunSplit(triggers, footageList, outputList);

        }

        public static List<string> RunSplit(List<Trigger> triggers, Dictionary<string, IList<Footage>> footageList, List<string> outputs)
        {
            var inputVid = "";
            var start = "";
            var outputFile = "output";
            var outputNum = 0;
            var outputSuf = ".mp4";
            TimeSpan duration = new TimeSpan(0, 0, before + after);
            TimeSpan tsbefore = new TimeSpan(0, 0, before);
            TimeSpan offset = new TimeSpan(0, 0, 0);

           


            //foreach (var t in triggers)
            //{
            //    foreach (var f in footageList)
            //    {
            //        foreach (var v in f.Value)
            //        {
            //            if (v.Within(t.CreateDate - offset))
            //            {
            //                //different files have different data rates much smaller than originals
            //                outputFile = "output";
            //                outputFile = outputFile + outputNum.ToString() + outputSuf;
            //                outputs.Add(outputFile);
            //                inputVid = v.FileName;
            //                start = ((t.CreateDate - (v.CreateDate + offset)) - tsbefore).ToString();
            //                Split(inputVid, start, duration.ToString(), outputFile);
            //                outputNum++;
            //            }
            //        }
            //    }
            //}

            return outputs;


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

        static void Main(string[] args)
        {

            string sourceFolder = args[0];
            int b, a;

            int.TryParse(args[1], out b);
            before = b;
            int.TryParse(args[2], out a);
            after = a;

            var footageFoldersList = new List<string>();




            footageFoldersList.Add(sourceFolder);
            foreach (var v in footageFoldersList)
            {
                var outputList = new List<string>();
                ProcessFolder(v, outputList);
            }

        }
    }
}



