using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

/// <summary>
/// An utility that binds the raw audio output to VFX graph.
/// </summary>
[AddComponentMenu("VFX/Property Binders/Audio Output Binder")]
[VFXBinder("Audio/Audio Output to AttributeMap")]
public class VFXAudioOutputBinder : VFXBinderBase
{
    public enum AudioSourceMode
    {
        AudioSource,
        AudioListener
    }

    public string CountProperty { get { return (string)m_CountProperty; } set { m_CountProperty = value; } }
    [VFXPropertyBinding("System.UInt32"), SerializeField, UnityEngine.Serialization.FormerlySerializedAs("m_CountParameter")]
    protected ExposedProperty m_CountProperty = "AudioCount";

    public string TextureProperty { get { return (string)m_TextureProperty; } set { m_TextureProperty = value; } }
    [VFXPropertyBinding("UnityEngine.Texture2D"), SerializeField, UnityEngine.Serialization.FormerlySerializedAs("m_TextureParameter")]
    protected ExposedProperty m_TextureProperty = "AudioTexture";

    public uint Samples = 256;
    public AudioSourceMode Mode = AudioSourceMode.AudioSource;
    public AudioSource AudioSource = null;



    private Texture2D m_Texture;
    private float[] m_AudioCache;
    private Color[] m_ColorCache;


    public override bool IsValid(VisualEffect component)
    {
        bool mode = Mode != AudioSourceMode.AudioSource || AudioSource != null;
        bool texture = component.HasTexture(TextureProperty);
        bool count = component.HasUInt(CountProperty);

        return mode && texture && count;
    }

    void UpdateTexture()
    {
        if (m_Texture == null || m_Texture.width != Samples)
        {
            if (m_Texture != null)
            {
                if (Application.isEditor)
                {
                    DestroyImmediate(m_Texture);
                }
                else
                {
                    Destroy(m_Texture);
                }
            }
            m_Texture = new Texture2D((int)Samples, 1, TextureFormat.RFloat, false)
            {
                name = "AudioSpectrum" + Samples
            };
            m_AudioCache = new float[Samples];
            m_ColorCache = new Color[Samples];
        }

        switch (Mode)
        {
            case AudioSourceMode.AudioListener:
                AudioListener.GetOutputData(m_AudioCache, 0);
                break;
            case AudioSourceMode.AudioSource:
                AudioSource.GetOutputData(m_AudioCache, 0);
                break;
        }

        for (int i = 0; i < Samples; i++)
        {
            m_ColorCache[i] = new Color(m_AudioCache[i], 0, 0, 0);
        }

        m_Texture.SetPixels(m_ColorCache);
        m_Texture.Apply();
    }

    public override void UpdateBinding(VisualEffect component)
    {
        UpdateTexture();
        component.SetTexture(TextureProperty, m_Texture);
        component.SetUInt(CountProperty, Samples);
    }

    public override string ToString()
    {
        return string.Format("Audio Spectrum : '{0} samples' -> {1}", m_CountProperty, (Mode == AudioSourceMode.AudioSource ? "AudioSource" : "AudioListener"));
    }

}
