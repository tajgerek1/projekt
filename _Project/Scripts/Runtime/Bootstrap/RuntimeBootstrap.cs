using UnityEngine;

namespace NocnaStraz
{
    /// <summary>
    /// Tworzy menedżer gry oraz prostego gracza FPS automatycznie po wciśnięciu Play.
    /// Dzięki temu projekt działa "out of the box".
    /// </summary>
    public static class RuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            EnsureGameManager();
            EnsurePlayer();
            // Środowisko jest teraz budowane w ParkWorld.Build() (v1.0.8),
            // więc nie tworzymy osobnego EnvironmentSpawner (żeby nie dublować obiektów).
        }

        private static void EnsureGameManager()
        {
            if (Object.FindAnyObjectByType<NightGameManager>() != null) return;

            var go = new GameObject("[NocnaStraz] NightGameManager");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<NightGameManager>();
        }

        private static void EnsurePlayer()
        {
            if (Object.FindAnyObjectByType<FpsPlayerController>() != null) return;

            // Spawn at a reasonable default position
            var player = new GameObject("[Player]");
            player.transform.position = new Vector3(0f, 1.2f, -24f);

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            // Simple visible body
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(1f, 1f, 1f);
            Object.Destroy(body.GetComponent<Collider>()); // avoid double-collision (CharacterController handles it)

            // Camera
            var camPivot = new GameObject("CameraPivot");
            camPivot.transform.SetParent(player.transform, false);
            camPivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);

            // Reuse an existing Main Camera from the scene if present (avoids black screen + 2x AudioListener warning)
            GameObject existingCamGO = null;
            try { existingCamGO = GameObject.FindGameObjectWithTag("MainCamera"); } catch { /* ignore */ }

            GameObject camGO;
            if (existingCamGO != null)
            {
                camGO = existingCamGO;
                camGO.transform.SetParent(camPivot.transform, false);
                camGO.transform.localPosition = Vector3.zero;
                camGO.transform.localRotation = Quaternion.identity;
                if (camGO.GetComponent<Camera>() == null) camGO.AddComponent<Camera>();
            }
            else
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                camGO.transform.SetParent(camPivot.transform, false);
                camGO.transform.localPosition = Vector3.zero;
                camGO.transform.localRotation = Quaternion.identity;
                camGO.AddComponent<Camera>();
            }

            // Ensure exactly one AudioListener (prefer the main camera)
            var anyListener = Object.FindAnyObjectByType<AudioListener>();
            if (camGO.GetComponent<AudioListener>() == null)
            {
                if (anyListener == null) camGO.AddComponent<AudioListener>();
            }

            // Controller
            var ctrl = player.AddComponent<FpsPlayerController>();
            // Assign pivot
            var field = typeof(FpsPlayerController).GetField("cameraPivot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(ctrl, camPivot.transform);
        }
    }
}
