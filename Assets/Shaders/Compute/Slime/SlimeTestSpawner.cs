using UnityEngine;

public class SlimeTestSpawner : MonoBehaviour
{
    public Slime Target;
    public string SpawnerTag;

    private void Update()
    {
        var objects = GameObject.FindGameObjectsWithTag(SpawnerTag);
        if (Target.SpawnPositions == null || Target.SpawnPositions.Length != objects.Length) Target.SpawnPositions = new Vector2[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            Target.SpawnPositions[i] = objects[i].transform.position.Flatland();
        }
    }
}