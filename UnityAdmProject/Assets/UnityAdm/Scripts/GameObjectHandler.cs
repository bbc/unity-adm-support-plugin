using System.Collections.Generic;
using System;
using UnityEngine;
using ADM;

namespace ADM
{
    public class GameObjectHandler
    {
        public Dictionary<UInt64, GameObject> gameObjects = new Dictionary<UInt64, GameObject>();
        public readonly object gameObjectsLock = new object();

        public GameObjectHandler()
        {
            if (DebugSettings.ModuleStartups) Debug.Log("Starting GameObjectHandler...");
        }
        public void shutdown()
        {
            if (DebugSettings.ModuleStartups) Debug.Log("Killing GameObjectHandler...");
            lock (gameObjectsLock)
            {
                foreach (var gameObject in gameObjects)
                {
                    UnityEngine.Object.Destroy(gameObject.Value);
                }
                gameObjects.Clear();
            }
        }

        public void configureNewItems(ref List<UInt64> itemsAwaitingConfig)
        {
            lock (GlobalState.metadataHandler.renderableItemsLock)
            {
                lock (gameObjectsLock)
                {
                    foreach (var id in itemsAwaitingConfig)
                    {
                        var typeDef = GlobalState.metadataHandler.renderableItems[id].typeDef;
                        GameObject newGo = null;

                        if (typeDef == AdmTypeDefs.OBJECTS && GlobalState.useVisualisations && GlobalState.objectVisualisation)
                        {
                            newGo = UnityEngine.Object.Instantiate(GlobalState.objectVisualisation) as GameObject;
                        }
                        else if (typeDef == AdmTypeDefs.DIRECTSPEAKERS && GlobalState.useVisualisations && GlobalState.dsVisualisation)
                        {
                            newGo = UnityEngine.Object.Instantiate(GlobalState.dsVisualisation) as GameObject;
                        }
                        else if (typeDef == AdmTypeDefs.HOA && GlobalState.useVisualisations && GlobalState.hoaVisualisation)
                        {
                            newGo = UnityEngine.Object.Instantiate(GlobalState.hoaVisualisation) as GameObject;
                        }
                        else
                        {
                            newGo = new GameObject();
                        }

                        newGo.name = "ADM: " + GlobalState.metadataHandler.renderableItems[id].name;
                        gameObjects.Add(id, newGo);
                    }
                }
            }
        }

        public void handleMetadataUpdate(MetadataUpdate metadataUpdate)
        {
            lock (gameObjectsLock)
            {
                if (gameObjects.ContainsKey(metadataUpdate.forId))
                {
                    GameObject gameObject = gameObjects[metadataUpdate.forId];
                    Renderer renderer = gameObject.GetComponent<Renderer>();
                    if (renderer)
                    {
                        renderer.enabled = ((metadataUpdate.metadataRunState == MetadataRunState.PROCESSING) || (metadataUpdate.metadataRunState == MetadataRunState.IN_GAP)) && metadataUpdate.audioRunning;
                    }
                    if (metadataUpdate.typeDef == AdmTypeDefs.DIRECTSPEAKERS)
                    {
                        // 3 DoF - Rotation Only
                        gameObject.transform.position = Camera.main.transform.position + metadataUpdate.inGamePosition;
                    }
                    else
                    {
                        // 6DoF - Game objects do not care about listener position
                        gameObject.transform.position = metadataUpdate.inGamePosition;
                    }
                }
            }
        }
    }
}

