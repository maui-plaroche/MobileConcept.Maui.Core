using System.Diagnostics;

namespace MobileConcept.Core.GameEngine.Maui;

/// <summary>
/// Relevé de cadence pour les mesures de performance : intervalle entre deux tics du minuteur,
/// pas de simulation joués par tic, intervalle entre deux dessins réels et temps de dessin de chaque couche.
/// Une ligne de synthèse est écrite sur la console toutes les 5 s (logcat sur Android, console de lancement sur iOS).
/// Tampons fixes : aucune allocation par image.
/// </summary>
internal sealed class FrameDiagnosticsLog
{
    private const int Capacity = 1024;
    private const double WindowSeconds = 5;
    private readonly double[] tickIntervals = new double[Capacity], paintIntervals = new double[Capacity],
        baseDraws = new double[Capacity], overlayDraws = new double[Capacity], scratch = new double[Capacity];
    private readonly int[] stepCounts = new int[4];
    private int ticks, paints, baseCount, overlayCount, baseWidth, baseHeight, overlayWidth, overlayHeight;
    private double windowStart = -1;
    private long lastPaint;
    private bool overlayScene;
    /// <summary>Mode carte graphique : « fond » devient la relecture OpenGL (vraies images affichées), « héros » l'enregistrement de la scène.</summary>
    public bool Gpu { get; set; }

    public void Tick(double now, double interval, int steps, bool hasOverlay)
    {
        lock (this) TickCore(now, interval, steps, hasOverlay);
    }
    public void Painted(bool overlay, double milliseconds, int width, int height)
    {
        lock (this) PaintedCore(overlay, milliseconds, width, height);
    }
    private void TickCore(double now, double interval, int steps, bool hasOverlay)
    {
        if (windowStart < 0) windowStart = now;
        overlayScene = hasOverlay;
        if (ticks < Capacity) tickIntervals[ticks++] = interval * 1000;
        stepCounts[Math.Min(steps, 3)]++;
        if (now - windowStart >= WindowSeconds) { Flush(now - windowStart); windowStart = now; }
    }

    private readonly double[] allocations = new double[Capacity];
    private int allocationCount;
    /// <summary>Octets alloués par le tic (fil d'interface) : c'est ce débit qui déclenche le ramasse-miettes.</summary>
    public void Allocated(long bytes)
    {
        lock (this) if (allocationCount < Capacity) allocations[allocationCount++] = bytes / 1024.0;
    }
    private void PaintedCore(bool overlay, double milliseconds, int width, int height)
    {
        if (overlay)
        {
            if (overlayCount < Capacity) overlayDraws[overlayCount++] = milliseconds;
            overlayWidth = width; overlayHeight = height;
            return;
        }
        if (baseCount < Capacity) baseDraws[baseCount++] = milliseconds;
        baseWidth = width; baseHeight = height;
        long now = Stopwatch.GetTimestamp();
        if (lastPaint != 0 && paints < Capacity) paintIntervals[paints++] = Stopwatch.GetElapsedTime(lastPaint, now).TotalMilliseconds;
        lastPaint = now;
    }

    private void Flush(double seconds)
    {
        int totalSteps = stepCounts[0] + stepCounts[1] + stepCounts[2] + stepCounts[3];
        string overlayPart = overlayCount > 0 ? FormattableString.Invariant($" | {(Gpu ? "enregistrement" : "héros")} {overlayWidth}x{overlayHeight} {Stats(overlayDraws, overlayCount)}") : "";
        string line = FormattableString.Invariant($"[FrameStats] {(overlayScene ? "carte" : "plateau")} {seconds:0.0}s | tics {ticks / seconds:0.0}/s intervalle {Stats(tickIntervals, ticks)} | pas par tic 0:{Percent(stepCounts[0], totalSteps)} 1:{Percent(stepCounts[1], totalSteps)} 2:{Percent(stepCounts[2], totalSteps)} 3+:{Percent(stepCounts[3], totalSteps)} | dessins {(paints + 1) / seconds:0.0}/s intervalle {Stats(paintIntervals, paints)} | {(Gpu ? "GPU" : "fond")} {baseWidth}x{baseHeight} {Stats(baseDraws, baseCount)}{overlayPart}");
        if (allocationCount > 0)
        {
            double total = 0; for (int i = 0; i < allocationCount; i++) total += allocations[i];
            line += FormattableString.Invariant($" | alloué par image {Stats(allocations, allocationCount).Replace(" ms", " Kio")}, {total / 1024 / seconds:0.00} Mio/s");
        }
        Console.WriteLine(line);
        ticks = paints = baseCount = overlayCount = allocationCount = 0;
        Array.Clear(stepCounts);
    }

    private static string Percent(int count, int total) => total == 0 ? "-" : FormattableString.Invariant($"{100.0 * count / total:0}%");

    private string Stats(double[] values, int count)
    {
        if (count == 0) return "-";
        Array.Copy(values, scratch, count);
        Array.Sort(scratch, 0, count);
        double At(double p) => scratch[Math.Min(count - 1, (int)(count * p))];
        return FormattableString.Invariant($"méd {At(.5):0.0} p90 {At(.9):0.0} p99 {At(.99):0.0} max {scratch[count - 1]:0.0} ms");
    }
}
