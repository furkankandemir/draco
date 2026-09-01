using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VFXSpawner : MonoBehaviour
{
    public Text fx_name;
    public GameObject[] effect;
    public float _TimeDel = 2.0f;
    private static int numSpawned = 0;
    Vector3 rootPos = new Vector3(0, 0, 0);
    public Camera sceneCamera;
    private int index;
    public Material[] bgMat;
    public MeshRenderer meshRenderer;
    // Start is called before the first frame update
    void Start()
    {
        effect = Resources.LoadAll<GameObject>("Prefabs");
        fx_name.text = effect[0].name;

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                rootPos = hit.point;
                rootPos.y = 0f;
                GameObject fx = Instantiate(effect[numSpawned], rootPos, Quaternion.identity);
                Destroy(fx, _TimeDel);
            }
        }
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            nextEf();
        }
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            prevEf();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            if (index < bgMat.Length - 1)
            {
                index++;
                meshRenderer.material = bgMat[index];
            }
            else if (index >= bgMat.Length - 1)
            {
                index = 0;
                meshRenderer.material = bgMat[index];
            }
        }
    }

    void changeTextLabel(int numSpawned)
    {
        fx_name.text = effect[numSpawned].name;

    }

    public void nextEf()
    {
        if (numSpawned < effect.Length - 1)
        {
            numSpawned++;
            changeTextLabel(numSpawned);
        }
        else if (numSpawned >= effect.Length - 1)
        {
            numSpawned = 0;
            changeTextLabel(numSpawned);
        }
    }

    public void prevEf()
    {
        if (numSpawned > 0)
        {
            numSpawned--;
            changeTextLabel(numSpawned);
        }
        else if (numSpawned == 0)
        {
            numSpawned = effect.Length - 1;
            changeTextLabel(numSpawned);
        }
    }
}
