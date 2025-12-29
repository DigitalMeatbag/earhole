namespace earhole.Services;

/// <summary>
/// Handles beat detection through multi-band energy analysis, BPM estimation, and peak detection
/// </summary>
public class BeatDetectionService
{
    // Sub-band analysis (frequency ranges)
    private const int SUB_BASS_BINS = 8;   // ~20-60 Hz (kick drum)
    private const int BASS_BINS = 15;      // ~60-250 Hz (bass guitar, low toms)
    private const int LOW_MID_BINS = 10;   // ~250-500 Hz (snare transients)
    
    private const float BEAT_THRESHOLD = 1.4f; // Threshold multiplier for beat detection
    private const float ENERGY_DECAY = 0.95f; // Slower decay to maintain sensitivity
    private const float VARIANCE_DECAY = 0.93f; // Slower variance decay
    private const float ONSET_FLUX_THRESHOLD = 0.15f; // Minimum energy increase
    private const int BPM_HISTORY_SIZE = 8; // Track last 8 beats
    
    private float averageEnergy = 0f;
    private float lastEnergy = 0f;
    private float energyVariance = 0f;
    private int beatCooldown = 0;
    private float estimatedBPM = 120f;
    private int dynamicCooldownFrames = 8;
    
    private readonly float[] energyHistory = new float[3];
    private int energyHistoryIndex = 0;
    private readonly Queue<DateTime> beatTimestamps = new();

    public bool IsBeat { get; private set; }
    public float EstimatedBPM => estimatedBPM;
    public float AverageEnergy => averageEnergy;

    public void ProcessSpectrum(float[] leftSpectrum, float[] rightSpectrum)
    {
        // Calculate sub-band energies
        int beatMaxBins = Math.Min(leftSpectrum.Length, rightSpectrum.Length);
        float subBassEnergy = 0f;
        float bassEnergy = 0f;
        float lowMidEnergy = 0f;
        
        // Sub-bass (kick drum range) - highest weight
        int subBassEnd = Math.Min(SUB_BASS_BINS, beatMaxBins);
        for (int i = 0; i < subBassEnd; i++)
        {
            subBassEnergy += (leftSpectrum[i] + rightSpectrum[i]) / 2f;
        }
        subBassEnergy /= Math.Max(1, subBassEnd);
        
        // Bass range (bass guitar, low toms)
        int bassEnd = Math.Min(SUB_BASS_BINS + BASS_BINS, beatMaxBins);
        for (int i = subBassEnd; i < bassEnd; i++)
        {
            bassEnergy += (leftSpectrum[i] + rightSpectrum[i]) / 2f;
        }
        bassEnergy /= Math.Max(1, bassEnd - subBassEnd);
        
        // Low-mid range (snare transients)
        int lowMidEnd = Math.Min(SUB_BASS_BINS + BASS_BINS + LOW_MID_BINS, beatMaxBins);
        for (int i = bassEnd; i < lowMidEnd; i++)
        {
            lowMidEnergy += (leftSpectrum[i] + rightSpectrum[i]) / 2f;
        }
        lowMidEnergy /= Math.Max(1, lowMidEnd - bassEnd);
        
        // Combine sub-bands with weighting (emphasize sub-bass)
        float currentEnergy = subBassEnergy * 0.6f + bassEnergy * 0.3f + lowMidEnergy * 0.1f;

        // Store in circular buffer for peak detection
        energyHistory[energyHistoryIndex] = currentEnergy;
        energyHistoryIndex = (energyHistoryIndex + 1) % energyHistory.Length;
        
        // Calculate instantaneous energy change (onset flux)
        float energyDelta = currentEnergy - lastEnergy;
        lastEnergy = currentEnergy;

        // Update variance for adaptive thresholding
        float diff = currentEnergy - averageEnergy;
        energyVariance = energyVariance * VARIANCE_DECAY + (diff * diff) * (1 - VARIANCE_DECAY);
        float stdDev = (float)Math.Sqrt(Math.Max(0, energyVariance));

        // Update running average with decay
        averageEnergy = averageEnergy * ENERGY_DECAY + currentEnergy * (1 - ENERGY_DECAY);

        // Decrement cooldown
        if (beatCooldown > 0) beatCooldown--;
        
        // Check if we're at a local peak (current > previous AND current > next)
        int prevIdx = (energyHistoryIndex + energyHistory.Length - 2) % energyHistory.Length;
        int currIdx = (energyHistoryIndex + energyHistory.Length - 1) % energyHistory.Length;
        bool isLocalPeak = currentEnergy >= energyHistory[prevIdx] && 
                           energyHistory[currIdx] >= energyHistory[prevIdx];

        // Detect beat using multiple criteria
        float dynamicThreshold = averageEnergy + stdDev * 0.5f;
        float onsetFluxThreshold = averageEnergy * ONSET_FLUX_THRESHOLD;
        
        if (beatCooldown == 0 && 
            currentEnergy > averageEnergy * BEAT_THRESHOLD &&
            energyDelta > onsetFluxThreshold &&
            currentEnergy > dynamicThreshold &&
            isLocalPeak)
        {
            IsBeat = true;
            
            // Track beat timing for BPM estimation
            DateTime now = DateTime.Now;
            beatTimestamps.Enqueue(now);
            if (beatTimestamps.Count > BPM_HISTORY_SIZE)
            {
                beatTimestamps.Dequeue();
            }
            
            // Estimate BPM from recent beat intervals
            if (beatTimestamps.Count >= 3)
            {
                var timestamps = beatTimestamps.ToArray();
                double totalSeconds = (timestamps[timestamps.Length - 1] - timestamps[0]).TotalSeconds;
                int intervals = timestamps.Length - 1;
                if (totalSeconds > 0 && intervals > 0)
                {
                    float beatsPerSecond = intervals / (float)totalSeconds;
                    float newBPM = beatsPerSecond * 60f;
                    
                    // Clamp to reasonable BPM range (60-200)
                    if (newBPM >= 60f && newBPM <= 200f)
                    {
                        // Smooth BPM estimation
                        estimatedBPM = estimatedBPM * 0.7f + newBPM * 0.3f;
                    }
                }
            }
            
            // Set adaptive cooldown based on estimated BPM
            float secondsPerBeat = 60f / estimatedBPM;
            dynamicCooldownFrames = Math.Max(6, (int)(secondsPerBeat * 60f * 0.4f));
            beatCooldown = dynamicCooldownFrames;
        }
        else
        {
            IsBeat = false;
        }
    }
}
