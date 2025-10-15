using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AdjustResForRT : MonoBehaviour
{
    public int m_Width = 2560;
    public int m_Height = 2560;

    private Camera m_Cam;
    private RenderTexture m_RT;

    // Start is called before the first frame update
    void Start()
    {
        m_Cam = GetComponent<Camera>();
        if(m_Cam == null)
        {
            Debug.LogError("AdjustResForRT must be attached to a GameObject with a Camera component.");
            return;
        }
        RebuildRT();
    }

    void RebuildRT()
    {
        // ÊÍ·Å¾ÉµÄ
        if (m_RT != null)
        {
            m_RT.Release();
            Destroy(m_RT);
        }

        m_RT = new RenderTexture(m_Width, m_Height, 24, RenderTextureFormat.Default);
        m_RT.name = $"{gameObject.name}_RT_{m_Width}x{m_Height}";
        m_RT.Create();

        m_Cam.targetTexture = m_RT;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
