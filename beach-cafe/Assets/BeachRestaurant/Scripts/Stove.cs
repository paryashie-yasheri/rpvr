using System.Collections.Generic;
using UnityEngine;

public class Stove : MonoBehaviour
{
    private readonly Dictionary<Collider, StoveCookable> contacts = new Dictionary<Collider, StoveCookable>();
    private readonly Dictionary<StoveCookable, int> counts = new Dictionary<StoveCookable, int>();
    private AudioSource audioSource;
    private AudioClip readyTone;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.playOnAwake = false;
        const int sampleRate = 44100;
        var samples = new float[sampleRate / 4];
        for (int i = 0; i < samples.Length; i++)
        {
            float envelope = Mathf.Min(1f, i / 220f, (samples.Length - i) / 220f);
            samples[i] = 0.25f * envelope * Mathf.Sin(2f * Mathf.PI * 880f * i / sampleRate);
        }
        readyTone = AudioClip.Create("Stove ready", samples.Length, 1, sampleRate, false);
        readyTone.SetData(samples, 0);
    }

    // The trigger collider only covers the grill's cooking area. Heat is applied
    // from collision callbacks on the solid cooking surface, so upper layers in
    // a stacked burger never cook just because they overlap that trigger volume.
    private void OnCollisionEnter(Collision collision)
    {
        RegisterContact(collision.collider);
    }

    private void OnCollisionExit(Collision collision)
    {
        UnregisterContact(collision.collider);
    }

    private void RegisterContact(Collider other)
    {
        var item = other.GetComponentInParent<StoveCookable>();
        if (!item || contacts.ContainsKey(other)) return;
        contacts.Add(other, item);
        if (counts.TryGetValue(item, out int count)) counts[item] = count + 1;
        else
        {
            counts.Add(item, 1);
            item.EnterStove(this);
            item.Ready += OnReady;
        }
    }

    private void UnregisterContact(Collider other)
    {
        if (!contacts.TryGetValue(other, out var item)) return;
        contacts.Remove(other);
        if (!item) return;
        if (--counts[item] > 0) return;
        counts.Remove(item);
        item.Ready -= OnReady;
        item.ExitStove(this);
    }

    private void OnReady() => audioSource.PlayOneShot(readyTone);

    private void OnDisable()
    {
        foreach (var item in counts.Keys)
        {
            if (!item) continue;
            item.Ready -= OnReady;
            item.ExitStove(this);
        }
        contacts.Clear();
        counts.Clear();
    }

    private void OnDestroy()
    {
        if (readyTone) Destroy(readyTone);
    }
}
