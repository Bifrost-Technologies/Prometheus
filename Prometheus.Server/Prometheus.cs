
using LLama;
using LLama.Common;
using LLamaSharp.SemanticKernel;
using LLamaSharp.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Prometheus.Server.compiler;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChatHistory = Microsoft.SemanticKernel.ChatCompletion.ChatHistory;
namespace Prometheus.Server
{
    public static class Prometheus
    {
        public static Dictionary<string, CompleteWork> CompletedWork = new Dictionary<string, CompleteWork>();
        public static Dictionary<string, string> JobWork = new Dictionary<string, string>();
        public static Dictionary<string, string> JobStatus = new Dictionary<string, string>();
        static string modelPath = @"UPDATE THIS PATH BEFORE RUNNING";
        static string promptsFolderPath = @"UPDATE THIS PATH BEFORE RUNNING";
        public static bool living = false;
        public static ModelParams? modelParams = null;
        public static LLamaWeights? Model = null;
        public static ChatHistory ChatMemory = null!;
        public static LLamaContext? Context = null;
        public static InteractiveExecutor? Executor = null;
        public static Kernel? Kernel = null!;
        public static IChatCompletionService? ChatService = null!;
        public static string SystemPrompt = string.Empty;
        public static void InitializeAI()
        {
            if(modelPath == "UPDATE THIS PATH BEFORE RUNNING")
            {
                throw new Exception("You must update the modelPath and promptsFolderPath variables in Prometheus.cs before running.");
            }
            modelParams = new ModelParams(modelPath)
            {
                ContextSize = 40960,
                GpuLayerCount = 75,
            };
            Model = LLamaWeights.LoadFromFile(modelParams);
            Context = Model.CreateContext(modelParams);
            Executor = new InteractiveExecutor(Context);
            ChatMemory = new ChatHistory();
            SystemPrompt = File.ReadAllText(promptsFolderPath+ "prometheus-system-prompt.txt");
            ChatMemory.AddSystemMessage(SystemPrompt);
            var setting = new LLamaSharpPromptExecutionSettings
            {
                MaxTokens = 80096, // The maximum number of tokens to generate in the response
                Temperature = 0.7f, // Controls the randomness of the output
                TopP = 0.9f, // Controls the diversity of the output
            };
            var kernel_builder = Kernel.CreateBuilder();
            // builder.Services.AddKeyedSingleton<ISemanticTextMemory>("bifrost-memory", memory);   
            kernel_builder.Services.AddSingleton<IChatCompletionService>(new LLamaSharpChatCompletion(Executor, setting));
            //builder.Plugins.AddFromType<TemporalAwareness>("TemporalAwareness");
            Kernel = kernel_builder.Build();
            ChatService = Kernel!.GetRequiredService<IChatCompletionService>();

        }
        public static async Task Living()
        {
            living = true;
            while (living)
            {
                if (JobStatus.Count > 0)
                {
                    foreach (var job in JobStatus.ToArray())
                    {
                        if (job.Value == "Pending")
                        {
                            Console.WriteLine("Job Found!");
                            ChatMemory = new ChatHistory();
                            ChatMemory.AddSystemMessage(SystemPrompt);
                            ChatMemory.AddUserMessage(JobWork[job.Key]);
                            var response = string.Empty;
                            DateTime start_timestamp = DateTime.Now;
                            CancellationTokenSource cts = new CancellationTokenSource();
                            await foreach (var reply in ChatService!.GetStreamingChatMessageContentsAsync(ChatMemory, kernel: Kernel, cancellationToken: cts.Token))
                            {
                                response += reply.Content!;
                                Console.Write(reply.Content);

                                //Timeout after 5 minutes
                                if ((DateTime.Now - start_timestamp).TotalSeconds > 300)
                                {
                                    Console.WriteLine("Job timed out");
                                    UpdateJobStatus(job.Key, "Timed out");
                                    response += "\n\n<Error>Job timed out</Error>";
                                    await cts.CancelAsync();
                                    break;
                                }
                            }
                            if (response.Contains("<Blueprint>"))
                            {
                                try
                                {
                                    var bps = response.Split("<Blueprint>").Length;
                                    var blueprint_data = response.Split("<Blueprint>")[1].Split("</Blueprint>")[0];
                                    if (bps > 2)
                                    {
                                        blueprint_data = response.Split("<Blueprint>")[2].Split("</Blueprint>")[2];
                                    }
                                    CompleteJob(job.Key, blueprint_data);
                                    BlueprintParser.CreateProjectFromBlueprint(job.Key, blueprint_data);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }
                            }
                        }
                    }
                }
               // Console.WriteLine("Living heartbeat...");
                await Task.Delay(1000);
            }
        }
        public static bool IsJobComplete(string jobId)
        {
            return CompletedWork.ContainsKey(jobId);
        }
        public static CompleteWork? GetCompletedJob(string jobId)
        {
            if (CompletedWork.ContainsKey(jobId))
            {
                return CompletedWork[jobId];
            }
            else
            {
                return null;
            }
        }
        public static void CompleteJob(string jobId, string blueprint)
        {
            if (JobWork.ContainsKey(jobId))
            {
                CompleteWork work = new CompleteWork() { Prompt = JobWork[jobId], Blueprint = blueprint };
                CompletedWork[jobId] = work;
                JobStatus[jobId] = "Complete";
                JobWork.Remove(jobId);
                SaveCompletedWork(jobId);
            }
        }
        public static bool IsJobPending(string jobId)
        {
            return JobStatus.ContainsKey(jobId) && JobStatus[jobId] == "Pending";
        }

        public static void AddJob(string jobId, string prompt)
        {
            if (!JobWork.ContainsKey(jobId))
            {
                CompletedWork.Add(jobId, new CompleteWork());
                JobWork.Add(jobId, prompt);
                JobStatus.Add(jobId, "Pending");
            }
        }
        public static string GetJobStatus(string jobId)
        {
            if (JobStatus.ContainsKey(jobId))
            {
                return JobStatus[jobId];
            }
            else
            {
                return "Job ID not found";
            }
        }
        public static void UpdateJobStatus(string jobId, string status)
        {
            if (JobStatus.ContainsKey(jobId))
            {
                JobStatus[jobId] = status;
            }
            else
            {
                JobStatus.Add(jobId, status);
            }
        }
        public static string GenerateJobId()
        {
            return Guid.NewGuid().ToString();
        }
        public static void SaveCompletedWork()
        {
            foreach (var job in CompletedWork)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(job.Value.Blueprint))
                    {
                        var path = Directory.GetCurrentDirectory() + $"/CompletedBlueprints/{job.Key}.json";
                        File.WriteAllText(path, JsonConvert.SerializeObject(job));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving completed work for job {job.Key}: {ex.Message}");
                }

            }
        }
        public static void SaveCompletedWork(string id)
        {
            if (CompletedWork.ContainsKey(id))
            {
                var job = CompletedWork[id];

                try
                {
                    if (!string.IsNullOrWhiteSpace(job.Blueprint))
                    {
                        var path = Directory.GetCurrentDirectory() + $"/CompletedBlueprints/{id}.json";
                        File.WriteAllText(path, JsonConvert.SerializeObject(job));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving completed work for job {id}: {ex.Message}");
                }
            }

        }
        public class CompleteWork
        {
            public string Prompt { get; set; } = string.Empty;
            public string Blueprint { get; set; } = string.Empty;
        }

        public static async Task GenerateZip(string response)
        {
            if (response.Contains("<Blueprint>"))
            {
                try
                {
                    var blueprint_data = response.Split("<Blueprint>")[1].Split("</Blueprint>")[0];
                    var files_data = blueprint_data.Split("<Files>")[1].Split("</Files>")[0].Trim();

                    Debug.WriteLine("Blueprint data: " + blueprint_data);
                    if (response.Contains("<Root>"))
                    {

                        var root_data = files_data.Split("<Root>")[1].Split("</Root>")[0].Trim();
                        var instruction_data = files_data.Split("<Instructions>")[1].Split("</Instructions>")[0].Trim();
                        var types_data = files_data.Split("<Types>")[1].Split("</Types>")[0].Trim();
                        Debug.WriteLine("Root data: " + root_data);
                        Debug.WriteLine("Instruction data: " + instruction_data);
                        Debug.WriteLine("Types data: " + types_data);

                        string project_id = Guid.NewGuid().ToString().Replace('-', '_');
                        Directory.CreateDirectory(Directory.GetCurrentDirectory()+@"\llm_projects\" + project_id + @"\src");
                        Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"\llm_projects\" + project_id + @"\src\instructions");
                        Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"\llm_projects\" + project_id + @"\src\state");
                        string src_folder = Directory.GetCurrentDirectory() + @"\llm_projects\" + project_id + @"\src\";
                        string instruction_folder = Directory.GetCurrentDirectory() + @"\llm_projects\" + project_id + @"\src\instructions\";
                        string state_folder = Directory.GetCurrentDirectory() + @"\llm_projects\" + project_id + @"\src\state\";
                        if (root_data.Contains(','))
                        {
                            try
                            {
                                var root = root_data.Trim().Split(',');
                                foreach (var name in root)
                                {

                                    var instruction_code = blueprint_data.Split($"<{name}>")[1].Split($"</{name}>")[0];
                                    File.WriteAllText(Path.Combine(src_folder, $"{name}"), instruction_code);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error writing root code: " + ex.Message);
                            }
                        }
                        else
                        {
                            try
                            {
                                var root_code = blueprint_data.Split($"<{root_data}>")[1].Split($"</{root_data}>")[0];
                                Debug.WriteLine("Root code: " + root_code);
                                File.WriteAllText(Path.Combine(src_folder, $"{root_data}"), root_code);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error writing root code: " + ex.Message);
                            }
                        }
                        if (instruction_data.Contains(','))
                        {
                            try
                            {
                                var instructions = instruction_data.Split(',');
                                foreach (var name in instructions)
                                {
                                    var instruction_code = blueprint_data.Split($"<{name.Trim()}>")[1].Split($"</{name.Trim()}>")[0];
                                    Debug.WriteLine(instruction_code);
                                    File.WriteAllText(Path.Combine(instruction_folder, $"{name.Trim()}"), instruction_code);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error writing instruction code: " + ex.Message);
                            }
                        }
                        else
                        {
                            try
                            {
                                var instruction_code = blueprint_data.Split($"<{instruction_data}>")[1].Split($"</{instruction_data}>")[0];
                                File.WriteAllText(Path.Combine(instruction_folder, $"{instruction_data}"), instruction_code);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error writing instruction code: " + ex.Message);
                            }
                        }
                        if (types_data.Contains(','))
                        {
                            var types = types_data.Split(',');
                            foreach (var name in types)
                            {
                                try
                                {
                                    var state_code = blueprint_data.Split($"<{name.Trim()}>")[1].Split($"</{name.Trim()}>")[0];
                                    File.WriteAllText(Path.Combine(state_folder, $"{name.Trim()}"), state_code);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("Error writing state code: " + ex.Message);
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                var state_code = blueprint_data.Split($"<{types_data}>")[1].Split($"</{types_data}>")[0];
                                File.WriteAllText(Path.Combine(state_folder, $"{types_data}"), state_code);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error writing state code: " + ex.Message);
                            }

                        }

                    }
                    else
                    {
                        string project_id = Guid.NewGuid().ToString().Replace('-', '_');
                        Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"\llm_projects\" + project_id + @"\src");
                        string src_folder = Directory.GetCurrentDirectory() + @"\llm_projects\" + project_id + @"\src\";
                        if (files_data.Contains(','))
                        {
                            try
                            {
                                var root = files_data.Trim().Split(',');
                                foreach (var name in root)
                                {

                                    var instruction_code = blueprint_data.Split($"<{name}>")[1].Split($"</{name}>")[0];
                                    File.WriteAllText(Path.Combine(src_folder, $"{name}"), instruction_code);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error writing root code: " + ex.Message);
                            }
                        }
                        else
                        {
                            try
                            {
                                var root_code = blueprint_data.Split($"<{files_data}>")[1].Split($"</{files_data}>")[0].Trim();
                                //  Debug.WriteLine("Root code: " + root_code);
                                File.WriteAllText(Path.Combine(src_folder, $"{files_data}"), root_code);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error writing root code: " + ex.Message);
                            }
                        }

                    }

                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }
    }
}
