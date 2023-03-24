using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SimaiSharp;
using SimaiSharp.Structures;

namespace SimaiClippy.Commands;

public class ChartCommands : BaseCommandModule
{
    static private String Cache { get
        {
            return Path.Join(AppDomain.CurrentDomain.BaseDirectory, "cache");
        } 
    }

    private async Task downloadFile(string filePath, string Url)
    {
        using (var client = new HttpClient())
        {
            Directory.CreateDirectory(Cache);

            using var file = new FileStream(filePath, FileMode.CreateNew);

            var resp = await client.GetAsync(Url);
            await resp.Content.CopyToAsync(file);
        }
    }

    private async Task<DiscordAttachment?> checkAttachmentInput(CommandContext ctx)
    {

        if (ctx.Message.Attachments.Count < 1)
        {
            await ctx.RespondAsync("Give me a chart!");
            return null;
        }

        var chartAttachment = ctx.Message.Attachments[0];
        if (!chartAttachment.MediaType.Contains("text/plain"))
        {
            await ctx.RespondAsync($"Input file is not text! Received media type of {chartAttachment.MediaType}");
            return null;
        }
        return chartAttachment;
    }

    [Command("breakdown")]
    [Description("Create a breakdown by note type of your chart")]
    public async Task BreakdownCommand(CommandContext ctx, params string[] charts)
    {
        var chartAttachment = await checkAttachmentInput(ctx);
        if (chartAttachment == null) { return; }

        var filename = Path.Join(Cache, chartAttachment.GetHashCode().ToString() + ".txt");
        await downloadFile(filename, chartAttachment.Url);

        var simaiFile = new SimaiFile(filename);
        var rawCharts = simaiFile.ToKeyValuePairs().Where(c => (charts.Length <= 0 || charts.Contains(c.Key)) && c.Key.Contains("inote_"));
        var results = new Dictionary<string, string>();
        foreach (var rawChart in rawCharts)
        {
            try
            {
                var chart = SimaiConvert.Deserialize(rawChart.Value);
                var breakdown = new Dictionary<string, int>()
                {
                    { NoteType.Tap.ToString(), 0 },
                    { $"EX_{NoteType.Tap}", 0 },
                    { NoteType.Hold.ToString(), 0 },
                    { $"EX_{NoteType.Hold}", 0 },
                    { NoteType.Slide.ToString(), 0 },
                    { NoteType.Touch.ToString(), 0 },
                    { NoteType.Break.ToString(), 0 },
                    { "BREAK", 0 },
                    { "BREAK_HOLD", 0 },
                    { "BREAK_SLIDE", 0 },
                    { "EX_BREAK", 0 },
                    { "EX_BREAK_HOLD", 0 },
                };
                for (var i = 0; i < chart.noteCollections.Count; i++)
                {
                    var collection = chart.noteCollections[i];
                    for (var j = 0; j < collection.Count; j++)
                    {
                        var note = collection[j];

                        breakdown[NoteType.Slide.ToString()] += note.slidePaths.Where(s => s.type == NoteType.Slide).Count();
                        breakdown["BREAK_SLIDE"] += note.slidePaths.Where(s => s.type == NoteType.Break).Count();
                        breakdown[NoteType.Break.ToString()] += note.slidePaths.Where(s => s.type == NoteType.Break).Count();

                        if (note.type != NoteType.Break && note.type != NoteType.ForceInvalidate)
                        {
                            breakdown[note.type.ToString()] += 1;
                            if (note.IsEx)
                            {
                                breakdown[$"EX_{note.type}"] += 1;
                            }
                        } else
                        {
                            var isHold = note.length != null;
                            breakdown[NoteType.Break.ToString()] += 1;
                            breakdown[(note.IsEx ? "EX_" : "") + "BREAK" + (isHold ? "_HOLD" : "")] += 1;
                        }
                    }
                }
                var maxCombo = Enum.GetValues(typeof(NoteType)).Cast<NoteType>().Sum(e => { breakdown.TryGetValue(e.ToString(), out var t); return t; });
                results.Add(rawChart.Key, $@"```
TOTAL           {maxCombo}

TAP             {breakdown[NoteType.Tap.ToString()]} ({breakdown[$"EX_{NoteType.Tap}"]} EX)
HOLD            {breakdown[NoteType.Hold.ToString()]} ({breakdown[$"EX_{NoteType.Hold}"]} EX)
SLIDE           {breakdown[NoteType.Slide.ToString()]}
TOUCH           {breakdown[NoteType.Touch.ToString()]}

Total breaks:   {breakdown[NoteType.Break.ToString()]}
- BREAK         {breakdown["BREAK"]}
- EX BREAK      {breakdown["EX_BREAK"]}
- BREAK HOLD    {breakdown["BREAK_HOLD"]}
- EX BREAK HOLD {breakdown["EX_BREAK_HOLD"]}
- BREAK SLIDE   {breakdown["BREAK_SLIDE"]}
```");
            }
            catch (Exception ex)
            {
                results.Add(rawChart.Key, $"```\n{ex.Message}\n```");
            }
        }

        var msg = string.Join('\n', results.Select(res => $"{res.Key}: {res.Value}"));
        await ctx.RespondAsync(msg);
        File.Delete(filename);
    }

    [Command("check")]
    [Description("Checks your chart for any syntax errors")]
    public async Task CheckCommand(CommandContext ctx, params string[] charts)
    {
        var chartAttachment = await checkAttachmentInput(ctx);
        if (chartAttachment == null) { return; }

        var filename = Path.Join(Cache, chartAttachment.GetHashCode().ToString() + ".txt");
        await downloadFile(filename, chartAttachment.Url);

        var simaiFile = new SimaiFile(filename);
        var rawCharts = simaiFile.ToKeyValuePairs().Where(c => (charts.Length <= 0 || charts.Contains(c.Key)) && c.Key.Contains("inote_"));
        var results = new Dictionary<string, string>();
        foreach (var rawChart in rawCharts)
        {
            try
            {
                var chart = SimaiConvert.Deserialize(rawChart.Value);
                results.Add(rawChart.Key, ":white_check_mark:");
            } catch (Exception ex)
            {
                results.Add(rawChart.Key, $"```\n{ex.Message}\n```");
            }
        }

        var msg = string.Join('\n', results.Select(res => $"{res.Key}: {res.Value}"));
        await ctx.RespondAsync(msg);
        File.Delete(filename);
    }
}
