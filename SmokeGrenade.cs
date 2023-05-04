using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


public class SmokeGrenadeScript : MonoBehaviour
{
    SmokeGrenadeState state = SmokeGrenadeState.Thrown; // to be set initially on UNTRIGGERED when used.


    [Header("Gameplay settings")]
    [SerializeField] float fuseTime = 1f;
    [SerializeField] float deploySpeed = 12f;
    [SerializeField] float lifeTime = 7f;
    [SerializeField] float decaySpeed = 3f;

    [Header("Mechanics settings")]   
    [SerializeField, Min(1)] int volume = 350;
    [SerializeField, Min(1)] int height = 4;
    [SerializeField, Min(0.001f)] float scale = 1f;
    [SerializeField] bool destroyOnFinnish = true;

    [Space]
    [SerializeField, Min(0)] int smokeLayerIndex = 1;
    [SerializeField] Material smokeMaterial = null;
    [SerializeField] bool debugMode = true;
   
    

    GameObject[,,] _cubeTensor;
    bool[,,] _cubeTensorPresence;

    GameObject _someCube;
    
    
    int X;
    int Y;
    int Z;

    private void Awake()
    {
        _someCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _someCube.name = "SmokeGrenade - SmokePrefab";
        _someCube.SetActive(false);
        _someCube.transform.localScale = _someCube.transform.localScale * 0.95f * scale;
        _someCube.GetComponent<BoxCollider>().isTrigger = true;

        if (debugMode)
        {
            if(smokeMaterial == null)
            {
                smokeMaterial = new Material(Shader.Find("Standard"));
                smokeMaterial.color = Color.gray;
            }
            
            _someCube.GetComponent<Renderer>().material = smokeMaterial;
        }
        else
        {
            var renderer = _someCube.GetComponent<Renderer>();
            Destroy(renderer);
        }


    }
    void Update()
    {
        if(state == SmokeGrenadeState.Thrown && fuseTime > 0)
        {
            fuseTime -= Time.deltaTime;
            if (fuseTime < 0)
            {
                state = SmokeGrenadeState.InDeployment;
                StartCoroutine(DeployGrenade());
            }
        }


        if(state == SmokeGrenadeState.Deployed && lifeTime > 0)
        {
            lifeTime -= Time.deltaTime;
            if(lifeTime < 0)
            {
                state = SmokeGrenadeState.InDecayment;
                StartCoroutine(DecayGrenade());

            }
        }
    }
    public void Trigger() => state = SmokeGrenadeState.Thrown;

    IEnumerator DeployGrenade()
    {
        // initialize matrix [X,Y,Z]
        X = volume / height;
        Y = height;
        Z = volume / height;

        _cubeTensor = new GameObject[X, Y, Z];
        _cubeTensorPresence = new bool[X, Y, Z];

        // initialize starting cube
        NewVolumeCube(new Vector3Int(X / 2, 0, Z / 2));

        while (volume > 0)
        {
            yield return new WaitForSeconds(1f / deploySpeed);

            // Find all adjacent free spots
            HashSet<Vector3Int> freeSpots = new HashSet<Vector3Int>();

            for (int i = 0; i < X; i++)
            {
                for (int j = 0; j < Y; j++)
                {
                    for (int k = 0; k < Z; k++)
                    {
                        if (_cubeTensorPresence[i, j, k] == false)
                            continue;

                        if (_cubeTensor[i, j, k].GetComponent<VolumeCubeComponent>().IsTouchingSomething == false)
                            freeSpots.AddRange(FreeSpotsAdjacentTo(new Vector3Int(i,j,k)));
                    }
                }
            }

            foreach (var item in freeSpots)
            {
                NewVolumeCube(item);
            }             
        }

        // Destroy cubes with less than 4 neighbours (not considering the diagonal ones)
        List<Vector3Int> margins = new List<Vector3Int>();
        for (int i = 0; i < X; i++)
        {
            for (int j = 0; j < Y; j++)
            {
                for (int k = 0; k < Z; k++)
                {
                    if (isEdge(i, j, k))
                        margins.Add(new Vector3Int(i, j, k));
                }
            }
        }

        foreach (var item in margins)
        {
            _cubeTensorPresence[item.x, item.y, item.z] = false;
            Destroy(_cubeTensor[item.x, item.y, item.z]);
        }

        state = SmokeGrenadeState.Deployed;
    }
    IEnumerator DecayGrenade()
    {
        // TO BE COMPLETED
        while(true)
        {
            List<Vector3Int> outsideCubes = new List<Vector3Int>();
            for (int i = 0; i < X; i++)
            {
                for (int j = 0; j < Y; j++)
                {
                    for (int k = 0; k < Z; k++)
                    {
                        if (_cubeTensorPresence[i,j,k] == true && isOutside(i, j, k))
                            outsideCubes.Add(new Vector3Int(i, j, k));
                    }
                }
            }

            if (outsideCubes.Count == 0)
                break;
            else
            {
                foreach (var item in outsideCubes)
                {
                    Destroy(_cubeTensor[item.x, item.y, item.z]);
                    _cubeTensor[item.x, item.y, item.z] = null;
                    _cubeTensorPresence[item.x, item.y, item.z] = false;
                }
            }

            yield return new WaitForSeconds(1f / decaySpeed);
        }
       
        _cubeTensorPresence = null;
        state = SmokeGrenadeState.Decayed;
        Destroy(_someCube);

        if (destroyOnFinnish)
            Destroy(this.gameObject);
    }
    Vector3 WorldPositionFromMatrix(Vector3Int matrixIndices)
    {
        // Smoke grenade is set to be on [X/2, 0, Z/2], we need to find the relative position of the cube position
        return (
                 new Vector3(matrixIndices.x - X / 2, matrixIndices.y, matrixIndices.z - Z / 2) +
                 new Vector3(0, scale / 2f, 0)
               )
               * scale + transform.position;
    }



    void NewVolumeCube(Vector3Int MatrixPosition)
    {
        var newCube = Instantiate(_someCube, WorldPositionFromMatrix(MatrixPosition), Quaternion.identity);
        newCube.SetActive(true);
        newCube.AddComponent<VolumeCubeComponent>();
        newCube.gameObject.layer = smokeLayerIndex; 
        
        volume--;

        _cubeTensor[MatrixPosition.x, MatrixPosition.y, MatrixPosition.z] = newCube;
        _cubeTensorPresence[MatrixPosition.x, MatrixPosition.y, MatrixPosition.z] = true;
    }
    List<Vector3Int> FreeSpotsAdjacentTo(Vector3Int location)
    {    
         var spots = new List<Vector3Int>();
         
         int x = location.x;
         int y = location.y;
         int z = location.z;
     
         for(int i = location.x - 1; i < location.x + 2; i++)
         {
             for (int j = location.y - 1; j < location.y + 2; j++)
             {
                 for (int k = location.z - 1; k < location.z + 2; k++)
                 {
                     try
                     {
                         if (_cubeTensor[i, j, k] == null && j < height)
                             spots.Add(new Vector3Int(i, j, k));
                     }
                     catch { }
                 }
             }
         }


        return spots;

    }
    bool isEdge(int X,  int Y, int Z)
    {
        // Is margin only if it has 4 or less neighbours
        if(Y == 0)
            return false;

        int nbs = 0;
        try
        {
            if (_cubeTensorPresence[X + 1, Y, Z] == true)
                nbs++;
        }
        catch { }
        try
        {
            if (_cubeTensorPresence[X - 1, Y, Z] == true)
                nbs++;
        }

        catch { }
        try
        {
            if (_cubeTensorPresence[X, Y + 1, Z] == true)
                nbs++;
        }
        catch { }
        try
        {
            if (_cubeTensorPresence[X, Y - 1, Z] == true)
                nbs++;
        }
        catch { }

        try
        {
            if (_cubeTensorPresence[X, Y, Z + 1] == true)
                nbs++;
        }
        catch { }
        try
        {
            if (_cubeTensorPresence[X, Y, Z - 1] == true)
                nbs++;
        }
        catch { }

        if (nbs < 5)
            return true;

        return false;
    }
    bool isOutside(int X, int Y, int Z)
    {
        try
        {
            if (_cubeTensorPresence[X + 1, Y, Z] == false)
                return true;
        }
        catch { }
        try
        {
            if (_cubeTensorPresence[X - 1, Y, Z] == false)
                return true;
        }

        catch { }
        try
        {
            if (_cubeTensorPresence[X, Y + 1, Z] == false)
                return true;
        }
        catch { }
        try
        {
            if (_cubeTensorPresence[X, Y - 1, Z] == false)
                return true;
        }
        catch { }

        try
        {
            if (_cubeTensorPresence[X, Y, Z + 1] == false)
                return true;
        }
        catch { }
        try
        {
            if (_cubeTensorPresence[X, Y, Z - 1] == false)
                return true;
        }
        catch { }

        return false;
    }
}

enum SmokeGrenadeState
{
    Untriggered,
    Thrown,
    InDeployment,
    Deployed,
    InDecayment,
    Decayed
}