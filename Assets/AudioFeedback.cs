using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(AudioSource))]
public class AudioFeedback : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    private Volume globalVolume;
    private Vignette vignette;

    private void Start()
    {
        try
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                throw new MissingComponentException("AudioFeedback requires an AudioSource.");
            }

        }
        catch (System.Exception exception)
        {
            Debug.LogError(exception.Message, this);
        }

        try
        {
            globalVolume = FindAnyObjectByType<Volume>();
            if (globalVolume == null)
            {
                throw new MissingComponentException("AudioFeedback could not find a global Volume.");
            }

            if (globalVolume.profile == null ||
                !globalVolume.profile.TryGet(out vignette) ||
                vignette == null)
            {
                throw new MissingComponentException("AudioFeedback could not find a Vignette in the global Volume profile.");
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    public void SetFeedback(float feedbackAmount, bool usePitch)
    {
        float amount = Mathf.Clamp01(feedbackAmount);

        try
        {
            if (audioSource == null)
            {
                throw new MissingComponentException("AudioFeedback cannot apply volume or pitch because its AudioSource is missing.");
            }

            audioSource.volume = amount;
            audioSource.pitch = usePitch ? amount : 1f;
        }
        catch (System.Exception exception)
        {
            Debug.LogError(exception.Message, this);
        }

        try
        {
            if (vignette == null)
            {
                throw new MissingComponentException("AudioFeedback cannot apply vignette intensity because the Vignette is missing.");
            }

            vignette.intensity.Override(amount);
        }
        catch (System.Exception exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }
}
