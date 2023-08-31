// ReSharper disable UnusedMember.Global
using Disqord;
using Disqord.Bot.Commands.Text;
using Qmmands;
using Qmmands.Text;
using SimaiSharp;
using SimaiSharp.Internal.Errors;
using SimaiSharp.Structures;
using static Disqord.Discord.Limits;

namespace SimaiClippy.Commands;

public class ChartCommands : DiscordTextModuleBase
{
    private static async Task<string> DownloadFile(string url)
    {
        using var client = new HttpClient();
        var resp = await client.GetAsync(url);
        return await resp.Content.ReadAsStringAsync();
    }

    private IResult? CheckAttachmentInput()
    {
        if (Context.Message.Attachments.Count < 1)
        {
            return Reply(new LocalMessage()
                .WithAllowedMentions(LocalAllowedMentions.None)
                .WithContent("Give me a chart!"));
        }

        var attachment = Context.Message.Attachments[0];

        // ReSharper disable once InvertIf
        if (attachment.ContentType == null || !attachment.ContentType.Contains("text/plain"))
        {
            return Reply(new LocalMessage()
                .WithAllowedMentions(LocalAllowedMentions.None)
                .WithContent($"Input file is not text! Received file of type `{attachment.ContentType}`")
            );
        }

        return null;
    }

    private static string HumanizeSimaiException(SimaiException ex)
    {
        switch (ex)
        {
            case InvalidSyntaxException:
                return $"Invalid location declaration (line {ex.line}, char {ex.character}).";
            case ScopeMismatchException castedEx:
            {
                var whatShouldBeAttachedTo = new List<string>();
                if ((castedEx.correctScope & ScopeMismatchException.ScopeType.Note) != 0)
                {
                    whatShouldBeAttachedTo.Add("notes");
                }

                if ((castedEx.correctScope & ScopeMismatchException.ScopeType.Slide) != 0)
                {
                    whatShouldBeAttachedTo.Add("slides");
                }

                return (castedEx.correctScope & ScopeMismatchException.ScopeType.Global) != 0
                    ? $"Items should be declared outside of note scope (line {castedEx.line}, char {castedEx.character})."
                    : $"Items should be attached to {string.Join(", or ", whatShouldBeAttachedTo)} (line {castedEx.line}, char {castedEx.character}).";
            }
            case UnsupportedSyntaxException:
                return $"Unsupported syntax used (line {ex.line}, char {ex.character}).";
            case UnterminatedSectionException:
                return $"Unterminated section (line {ex.line}, char {ex.character}).";
            default:
                return $"Unknown error (line {ex.line}, char {ex.character}).";
        }
    }

    [TextCommand("check", "c")]
    public async Task<IResult> Check(string[]? charts = null)
    {
        charts ??= Array.Empty<string>();

        var check = CheckAttachmentInput();
        if (check != null)
        {
            return check;
        }

        var attachment = Context.Message.Attachments[0];
        string content;
        try
        {
            content = await DownloadFile(attachment.Url);
        }
        catch (InvalidOperationException)
        {
            return Reply(new LocalMessage()
                .WithContent("Couldn't read the chart. Try saving it as UTF-8 (without BOM) and try again.")
                .WithAllowedMentions(LocalAllowedMentions.None));
        }

        var simaiFile = new SimaiFile(content);
        var rawCharts = simaiFile.ToKeyValuePairs()
            .Where(c => (charts.Length == 0 || charts.Contains(c.Key)) && c.Key.Contains("inote_"));
        var results = new Dictionary<string, string>();

        foreach (var rawChart in rawCharts)
        {
            var message = ":white_check_mark:";
            try
            {
                SimaiConvert.Deserialize(rawChart.Value);
            }
            catch (SimaiException ex)
            {
                message = $"```\n{HumanizeSimaiException(ex)}\n```";
            }
            catch (Exception ex)
            {
                message = $"An error occured in SimaiSharp. Your chart is probably super fucked up.\n```\n{ex}\n```";
            }

            results.Add(rawChart.Key, message);
        }

        var msg = string.Join('\n', results.Select(res => $"{res.Key}: {res.Value}"));
        return Reply(new LocalMessage()
            .WithContent(msg)
            .WithAllowedMentions(LocalAllowedMentions.None));
    }

    [TextCommand("breakdown")]
    public async Task<IResult> Breakdown(string[]? charts = null)
    {
        charts ??= Array.Empty<string>();

        var check = CheckAttachmentInput();
        if (check != null)
        {
            return check;
        }

        var attachment = Context.Message.Attachments[0];
        string content;
        try
        {
            content = await DownloadFile(attachment.Url);
        }
        catch (InvalidOperationException)
        {
            return Reply(new LocalMessage()
                .WithContent("Couldn't read the chart. Try saving it as UTF-8 (without BOM) and try again.")
                .WithAllowedMentions(LocalAllowedMentions.None));
        }

        var simaiFile = new SimaiFile(content);
        var rawCharts = simaiFile.ToKeyValuePairs()
            .Where(c => (charts.Length == 0 || charts.Contains(c.Key)) && c.Key.Contains("inote_"));
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
                    { "HANABI_TOUCH", 0 },
                    { NoteType.Break.ToString(), 0 },
                    { "BREAK", 0 },
                    { "BREAK_HOLD", 0 },
                    { "BREAK_SLIDE", 0 },
                    { "EX_BREAK", 0 },
                    { "EX_BREAK_HOLD", 0 },
                };
                foreach (var collection in chart.NoteCollections)
                {
                    foreach (var note in collection)
                    {
                        breakdown[NoteType.Slide.ToString()] += note.slidePaths.Count(s => s.type == NoteType.Slide);
                        breakdown["BREAK_SLIDE"] += note.slidePaths.Count(s => s.type == NoteType.Break);
                        breakdown[NoteType.Break.ToString()] += note.slidePaths.Count(s => s.type == NoteType.Break);

                        if (note.type != NoteType.Break && note.type != NoteType.ForceInvalidate)
                        {
                            breakdown[note.type.ToString()] += 1;
                            if (note.IsEx)
                            {
                                breakdown[$"EX_{note.type}"] += 1;
                            }
                            if (note.location.group != NoteGroup.Tap && (note.styles & NoteStyles.Fireworks) != 0)
                            {
                                breakdown["HANABI_TOUCH"] += 1;
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
                results.Add(rawChart.Key, $"""
                                           ```
                                           TOTAL           {maxCombo}

                                           TAP             {breakdown[NoteType.Tap.ToString()]} ({breakdown[$"EX_{NoteType.Tap}"]} EX)
                                           HOLD            {breakdown[NoteType.Hold.ToString()]} ({breakdown[$"EX_{NoteType.Hold}"]} EX)
                                           SLIDE           {breakdown[NoteType.Slide.ToString()]}
                                           TOUCH           {breakdown[NoteType.Touch.ToString()]} ({breakdown[$"HANABI_TOUCH"]} fireworks)

                                           Total breaks:   {breakdown[NoteType.Break.ToString()]}
                                           - BREAK         {breakdown["BREAK"]}
                                           - EX BREAK      {breakdown["EX_BREAK"]}
                                           - BREAK HOLD    {breakdown["BREAK_HOLD"]}
                                           - EX BREAK HOLD {breakdown["EX_BREAK_HOLD"]}
                                           - BREAK SLIDE   {breakdown["BREAK_SLIDE"]}
                                           ```
                                           """);
            }
            catch (SimaiException ex)
            {
                results.Add(rawChart.Key, $"```\n{HumanizeSimaiException(ex)}\n```");
            }
            catch (Exception ex)
            {
                results.Add(rawChart.Key, $"An error occured in SimaiSharp. Your chart is probably super fucked up.\n```\n{ex}\n```");
            }
        }

        var msg = string.Join('\n', results.Select(res => $"{res.Key}: {res.Value}"));
        return Reply(new LocalMessage()
            .WithContent(msg)
            .WithAllowedMentions(LocalAllowedMentions.None));
    }
}