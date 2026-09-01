using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hovl
{
    // This script is responsible for skills, VFX, target markers, UI aim, sounds, ultimate and camera shake.
    // It requires HS_MovementInput on the same object.
    [RequireComponent(typeof(HS_MovementInput))]
    public class HS_AbilitiesInput : MonoBehaviour
    {
        [Header("Movement Script")]
        public HS_MovementInput movementInput;

        [Space]
        [Header("Effects")]
        public GameObject TargetMarker;
        public GameObject TargetMarker2;
        public GameObject[] Prefabs;
        public GameObject[] PrefabsCast;
        public GameObject[] UltimatePrefab;

        public float[] castingTime; // If 0 - can loop, if > 0 - one shot time.

        public LayerMask collidingLayer = ~0; // Target marker can only collide with scene layer.

        private bool canUlt = false;
        private bool useUlt = false;
        private ParticleSystem currEffect;
        private ParticleSystem Effect;
        private bool casting;
        private Transform parentObject;
        private Transform parentForUlt;
        private int currNumber;
        private bool fastSkillrefresh = false;

        [Space]
        [Header("Canvas")]
        public GameObject[] ultIcons;
        public Image aim;
        public Vector2 uiOffset;
        public List<Transform> screenTargets = new List<Transform>();
        public Transform FirePoint;
        public float fireRate = 0.1f;

        private Transform target;
        private bool activeTarger = false;
        private float fireCountdown = 0f;
        private bool rotateState = false;
        private float secondLayerWeight = 0;

        [Space]
        [Header("Sound effects")]
        private AudioSource soundComponent; // Play audio from Prefabs.
        private AudioClip clip;
        private AudioSource soundComponentCast; // Play audio from PrefabsCast.
        private AudioSource soundComponentUlt; // Play audio from UltimatePrefab.

        [Space]
        [Header("Camera Shaker script")]
        public HS_CameraShaker cameraShaker;

        private Animator Anim => movementInput != null ? movementInput.anim : null;

        private Camera Cam
        {
            get
            {
                if (movementInput != null && movementInput.cam != null)
                    return movementInput.cam;

                return Camera.main;
            }
        }

        private void Awake()
        {
            if (!movementInput)
                movementInput = GetComponent<HS_MovementInput>();
        }

        private void Start()
        {
            casting = false;

            if (Prefabs != null && Prefabs.Length > 8 && Prefabs[8] && Prefabs[8].GetComponent<AudioSource>())
            {
                soundComponent = Prefabs[8].GetComponent<AudioSource>();
            }
        }

        private void Update()
        {
            if (screenTargets == null || screenTargets.Count == 0 || aim == null)
                return;

            target = screenTargets[targetIndex()];

            if (Input.GetMouseButtonDown(1) && casting == true)
            {
                casting = false;
            }

            if (Input.GetKeyDown("1"))
            {
                if (canUlt)
                    useUlt = true;
                else
                    StartCoroutine(PreCast(0));
            }

            if (Input.GetKeyDown("2") && casting == false)
            {
                if (canUlt)
                    useUlt = true;
                else if (!fastSkillrefresh)
                    StartCoroutine(FastPlay(1));
            }

            if (Input.GetKeyDown("3"))
            {
                StartCoroutine(PreCast(2));
            }

            if (Input.GetKeyDown("4"))
            {
                StartCoroutine(PreCast(3));
            }

            if (Input.GetKeyDown("z"))
            {
                StartCoroutine(FrontAttack(4));
            }

            if (Input.GetKeyDown("x"))
            {
                StartCoroutine(FrontAttack(5));
            }

            if (Input.GetKeyDown("c"))
            {
                if (canUlt)
                    useUlt = true;
                else
                    StartCoroutine(PreCast(6));
            }

            if (Input.GetKeyDown("v"))
            {
                if (canUlt)
                    useUlt = true;
                else
                    StartCoroutine(FrontAttack(7));
            }

            UserInterface();

            if (movementInput != null && !movementInput.canMove)
                return;

            if (Input.GetMouseButton(0) && aim.enabled == true && activeTarger)
            {
                if (rotateState == false)
                {
                    StartCoroutine(RotateToTarget(fireRate, target.position));
                }

                secondLayerWeight = Mathf.Lerp(secondLayerWeight, 1, Time.deltaTime * 10);

                if (fireCountdown <= 0f)
                {
                    GameObject projectile = Instantiate(PrefabsCast[8], FirePoint.position, FirePoint.rotation);
                    projectile.GetComponent<HS_TargetProjectile>().UpdateTarget(target, (Vector3)uiOffset);

                    Effect = Prefabs[8].GetComponent<ParticleSystem>();
                    Effect.Play();

                    if (Prefabs[8].GetComponent<AudioSource>())
                    {
                        soundComponent = Prefabs[8].GetComponent<AudioSource>();
                        clip = soundComponent.clip;
                        soundComponent.PlayOneShot(clip);
                    }

                    if (cameraShaker)
                        StartCoroutine(cameraShaker.Shake(0.1f, 2, 0.2f, 0));

                    fireCountdown = 0;
                    fireCountdown += fireRate;
                }
            }
            else
            {
                secondLayerWeight = Mathf.Lerp(secondLayerWeight, 0, Time.deltaTime * 10);
            }

            fireCountdown -= Time.deltaTime;

            if (Input.GetMouseButtonDown(1) && aim.enabled == true && activeTarger)
            {
                if (rotateState == false)
                {
                    StartCoroutine(RotateToTarget(fireRate, target.position));
                }

                secondLayerWeight = Mathf.Lerp(secondLayerWeight, 1, Time.deltaTime * 10);

                GameObject buff = Instantiate(PrefabsCast[9], target.position, target.rotation);
                buff.transform.parent = target;

                ParticleSystem buffPS = buff.GetComponent<ParticleSystem>();
                Destroy(buff, buffPS.main.duration);

                Effect = Prefabs[9].GetComponent<ParticleSystem>();
                Effect.Play();

                if (Prefabs[9].GetComponent<AudioSource>())
                {
                    soundComponent = Prefabs[9].GetComponent<AudioSource>();
                    clip = soundComponent.clip;
                    soundComponent.PlayOneShot(clip);
                }

                if (cameraShaker)
                    StartCoroutine(cameraShaker.Shake(0.15f, 2, 0.2f, 0));
            }
            else
            {
                secondLayerWeight = Mathf.Lerp(secondLayerWeight, 0, Time.deltaTime * 10);
            }

            if (Anim && Anim.layerCount > 1)
            {
                Anim.SetLayerWeight(1, secondLayerWeight);
            }
        }

        public IEnumerator FastPlay(int EffectNumber)
        {
            fastSkillrefresh = true;

            Effect = Prefabs[EffectNumber].GetComponent<ParticleSystem>();
            Effect.Play();

            Transform parentPlace = PrefabsCast[EffectNumber].transform.parent;
            PrefabsCast[EffectNumber].transform.parent = null;

            currEffect = PrefabsCast[EffectNumber].GetComponent<ParticleSystem>();
            currEffect.Play();

            if (Prefabs[EffectNumber].GetComponent<AudioSource>())
            {
                soundComponent = Prefabs[EffectNumber].GetComponent<AudioSource>();
                var clip = soundComponent.clip;
                soundComponent.PlayOneShot(clip);
            }

            if (PrefabsCast[EffectNumber].GetComponent<AudioSource>())
            {
                soundComponentCast = PrefabsCast[EffectNumber].GetComponent<AudioSource>();
                var clip = soundComponentCast.clip;
                soundComponentCast.PlayOneShot(clip);
            }

            if (EffectNumber == 1 && cameraShaker)
                StartCoroutine(cameraShaker.Shake(0.3f, 5, 0.5f, 0));

            if (UltimatePrefab[EffectNumber] != null)
                StartCoroutine(Ult(EffectNumber, 0f, 1.5f, new Vector3(0, 0, 0), transform.rotation, false));

            yield return new WaitForSeconds(castingTime[EffectNumber]);

            PrefabsCast[EffectNumber].transform.parent = parentPlace;
            PrefabsCast[EffectNumber].transform.position = parentPlace.position;

            fastSkillrefresh = false;
        }

        public IEnumerator Ult(int EffectNumber, float enableTime, float dissableTime, Vector3 pivotPosition, Quaternion pivotRotation, bool ChangePos)
        {
            yield return new WaitForSeconds(enableTime);

            canUlt = true;

            if (ultIcons != null && ultIcons.Length > EffectNumber && ultIcons[EffectNumber])
                ultIcons[EffectNumber].SetActive(true);

            while (true)
            {
                dissableTime -= Time.deltaTime;

                if (UltimatePrefab[EffectNumber] && useUlt)
                {
                    if (ChangePos == true)
                    {
                        parentForUlt = UltimatePrefab[EffectNumber].transform.parent;
                        UltimatePrefab[EffectNumber].transform.parent = null;

                        if (pivotPosition != new Vector3(1, 1, 1))
                            UltimatePrefab[EffectNumber].transform.position = pivotPosition;

                        UltimatePrefab[EffectNumber].transform.rotation = pivotRotation;
                    }

                    if (UltimatePrefab[EffectNumber].GetComponent<AudioSource>())
                    {
                        soundComponentUlt = UltimatePrefab[EffectNumber].GetComponent<AudioSource>();
                        soundComponentUlt.Play(0);
                    }

                    ParticleSystem ultPS = UltimatePrefab[EffectNumber].GetComponent<ParticleSystem>();
                    ultPS.Play();

                    if (cameraShaker)
                    {
                        if (EffectNumber == 0) StartCoroutine(cameraShaker.Shake(0.4f, 5, 0.35f, 0.1f));
                        if (EffectNumber == 1) StartCoroutine(cameraShaker.Shake(0.15f, 2, 0.2f, 0));
                        if (EffectNumber == 6) StartCoroutine(cameraShaker.Shake(0.2f, 7, 3, 0));
                        if (EffectNumber == 7) StartCoroutine(cameraShaker.Shake(0.55f, 7.5f, 0.35f, 0));
                    }

                    if (ultIcons != null && ultIcons.Length > EffectNumber && ultIcons[EffectNumber])
                        ultIcons[EffectNumber].SetActive(false);

                    canUlt = false;
                    useUlt = false;

                    yield return new WaitForSeconds(ultPS.main.duration);

                    if (ChangePos == true)
                    {
                        UltimatePrefab[EffectNumber].transform.parent = parentForUlt;
                        UltimatePrefab[EffectNumber].transform.localPosition = new Vector3(0, 0, 0);
                    }

                    yield break;
                }

                if (dissableTime <= 0)
                {
                    if (ultIcons != null && ultIcons.Length > EffectNumber && ultIcons[EffectNumber])
                        ultIcons[EffectNumber].SetActive(false);

                    canUlt = false;
                    useUlt = false;

                    yield break;
                }

                yield return null;
            }
        }

        private void UserInterface()
        {
            if (!target || !Cam)
                return;

            Vector3 screenCenter = new Vector3(Screen.width, Screen.height, 0) / 2;
            Vector3 screenPos = Cam.WorldToScreenPoint(target.position + (Vector3)uiOffset);
            Vector3 CornerDistance = screenPos - screenCenter;
            Vector3 absCornerDistance = new Vector3(
                Mathf.Abs(CornerDistance.x),
                Mathf.Abs(CornerDistance.y),
                Mathf.Abs(CornerDistance.z)
            );

            if (absCornerDistance.x < screenCenter.x / 3 &&
                absCornerDistance.y < screenCenter.y / 3 &&
                screenPos.x > 0 &&
                screenPos.y > 0 &&
                screenPos.z > 0 &&
                !Physics.Linecast(transform.position + (Vector3)uiOffset, target.position + (Vector3)uiOffset * 2, collidingLayer))
            {
                aim.transform.position = Vector3.MoveTowards(aim.transform.position, screenPos, Time.deltaTime * 3000);

                if (!activeTarger)
                    activeTarger = true;
            }
            else
            {
                aim.transform.position = Vector3.MoveTowards(aim.transform.position, screenCenter, Time.deltaTime * 3000);

                if (activeTarger)
                    activeTarger = false;
            }
        }

        public void MainSoundPlay()
        {
            if (!soundComponent)
                return;

            clip = soundComponent.clip;
            soundComponent.PlayOneShot(clip);
        }

        public void CastSoundPlay()
        {
            if (!soundComponentCast)
                return;

            soundComponentCast.Play(0);
        }

        public IEnumerator RotateToTarget(float rotatingTime, Vector3 targetPoint)
        {
            rotateState = true;

            if (movementInput)
                yield return StartCoroutine(movementInput.RotateToTarget(rotatingTime, targetPoint));

            rotateState = false;
        }

        public IEnumerator FrontAttack(int EffectNumber)
        {
            if (TargetMarker2 && casting == false)
            {
                aim.enabled = false;
                TargetMarker2.SetActive(true);

                while (true)
                {
                    var forwardCamera = Cam.transform.forward;
                    forwardCamera.y = 0.0f;

                    TargetMarker2.transform.rotation = Quaternion.LookRotation(forwardCamera);
                    var vecPos = transform.position + forwardCamera * 4;

                    if (Input.GetMouseButtonDown(0) && casting == false)
                    {
                        casting = true;

                        if (movementInput)
                            movementInput.BlockMovement();

                        TargetMarker2.SetActive(false);

                        if (rotateState == false)
                        {
                            StartCoroutine(RotateToTarget(1, vecPos));
                        }

                        if (Anim)
                            Anim.SetTrigger("FrontAttack");

                        if (cameraShaker)
                            StartCoroutine(cameraShaker.Shake(0.4f, 7, 0.45f, 1f));

                        if (Prefabs[EffectNumber].GetComponent<AudioSource>())
                        {
                            soundComponent = Prefabs[EffectNumber].GetComponent<AudioSource>();
                            MainSoundPlay();
                        }

                        yield return new WaitForSeconds(1);

                        foreach (var component in Prefabs[EffectNumber].GetComponentsInChildren<HS_FrontAttack>())
                        {
                            component.PrepeareAttack(vecPos);
                        }

                        if (UltimatePrefab[EffectNumber] != null)
                        {
                            if (EffectNumber == 7)
                            {
                                StartCoroutine(Ult(EffectNumber, 0f, 1.5f, new Vector3(1, 1, 1), Quaternion.LookRotation(forwardCamera), true));
                            }
                            else
                            {
                                StartCoroutine(Ult(EffectNumber, 0f, 1.5f, new Vector3(0, 0, 0), Quaternion.LookRotation(forwardCamera), false));
                            }
                        }

                        yield return new WaitForSeconds(castingTime[EffectNumber]);

                        StopCasting(EffectNumber);
                        aim.enabled = true;

                        yield break;
                    }
                    else if (Input.GetMouseButtonDown(1))
                    {
                        TargetMarker2.SetActive(false);
                        aim.enabled = true;

                        yield break;
                    }

                    yield return null;
                }
            }
        }

        public IEnumerator PreCast(int EffectNumber)
        {
            if (PrefabsCast[EffectNumber] && TargetMarker)
            {
                while (true)
                {
                    aim.enabled = false;
                    TargetMarker.SetActive(true);

                    var forwardCamera = Cam.transform.forward;
                    forwardCamera.y = 0.0f;

                    RaycastHit hit;
                    Ray ray = new Ray(Cam.transform.position + new Vector3(0, 2, 0), Cam.transform.forward);

                    bool hasHit = Physics.Raycast(ray, out hit, Mathf.Infinity, collidingLayer);

                    if (hasHit)
                    {
                        TargetMarker.transform.position = hit.point;
                        TargetMarker.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.LookRotation(forwardCamera);
                    }
                    else
                    {
                        aim.enabled = true;
                        TargetMarker.SetActive(false);
                    }

                    if (Input.GetMouseButtonDown(0) && casting == false && hasHit)
                    {
                        aim.enabled = true;
                        TargetMarker.SetActive(false);
                        soundComponentCast = null;

                        if (rotateState == false)
                        {
                            StartCoroutine(RotateToTarget(1, hit.point));
                        }

                        casting = true;

                        PrefabsCast[EffectNumber].transform.position = hit.point;
                        PrefabsCast[EffectNumber].transform.rotation = Quaternion.LookRotation(forwardCamera);

                        parentObject = PrefabsCast[EffectNumber].transform.parent;
                        PrefabsCast[EffectNumber].transform.parent = null;

                        currEffect = PrefabsCast[EffectNumber].GetComponent<ParticleSystem>();
                        Effect = Prefabs[EffectNumber].GetComponent<ParticleSystem>();

                        if (Prefabs[EffectNumber].GetComponent<AudioSource>())
                        {
                            soundComponent = Prefabs[EffectNumber].GetComponent<AudioSource>();
                        }

                        if (PrefabsCast[EffectNumber].GetComponent<AudioSource>())
                        {
                            soundComponentCast = PrefabsCast[EffectNumber].GetComponent<AudioSource>();
                        }

                        StartCoroutine(OnCast(EffectNumber));
                        StartCoroutine(Attack(EffectNumber));

                        if (UltimatePrefab[EffectNumber] != null)
                        {
                            StartCoroutine(Ult(EffectNumber, 0.5f, castingTime[EffectNumber], hit.point, Quaternion.LookRotation(forwardCamera), true));
                        }

                        yield break;
                    }
                    else if (Input.GetMouseButtonDown(1))
                    {
                        aim.enabled = true;
                        TargetMarker.SetActive(false);

                        yield break;
                    }

                    yield return null;
                }
            }
            else if (casting == false)
            {
                Effect = Prefabs[EffectNumber].GetComponent<ParticleSystem>();

                if (Prefabs[EffectNumber].GetComponent<AudioSource>())
                {
                    soundComponent = Prefabs[EffectNumber].GetComponent<AudioSource>();
                }

                casting = true;

                StartCoroutine(Attack(EffectNumber));

                yield break;
            }
        }

        private IEnumerator OnCast(int EffectNumber)
        {
            while (true)
            {
                if (casting)
                {
                    if (castingTime[EffectNumber] == 0)
                    {
                        currEffect.Play();

                        if (soundComponentCast)
                        {
                            CastSoundPlay();
                        }

                        yield return new WaitForSeconds(1f);
                    }
                    else
                    {
                        currEffect.Play();

                        if (cameraShaker)
                        {
                            if (EffectNumber == 0) StartCoroutine(cameraShaker.Shake(0.2f, 5, 2, 1.5f));
                            if (EffectNumber == 3) StartCoroutine(cameraShaker.Shake(0.6f, 6, 0.3f, 1.45f));
                        }

                        if (soundComponentCast)
                        {
                            CastSoundPlay();
                        }

                        yield return new WaitForSeconds(castingTime[EffectNumber]);

                        yield break;
                    }
                }
                else
                {
                    yield break;
                }
            }
        }

        public IEnumerator Attack(int EffectNumber)
        {
            if (movementInput)
                movementInput.BlockMovement();

            while (true)
            {
                if (casting)
                {
                    if (castingTime[EffectNumber] == 0)
                    {
                        if (EffectNumber == 2)
                        {
                            if (Anim)
                                Anim.SetTrigger("Attack1");

                            if (cameraShaker)
                                StartCoroutine(cameraShaker.Shake(0.2f, 6, 1.5f, 0));
                        }

                        Effect.Play();

                        if (soundComponent)
                        {
                            MainSoundPlay();
                        }

                        yield return new WaitForSeconds(0.9f);
                    }
                    else
                    {
                        if (EffectNumber == 0 || EffectNumber == 6)
                        {
                            if (Anim)
                                Anim.SetTrigger("Attack1");

                            if (cameraShaker)
                            {
                                if (EffectNumber == 0) StartCoroutine(cameraShaker.Shake(0.2f, 5, 3, 0));
                                if (EffectNumber == 6) StartCoroutine(cameraShaker.Shake(0.45f, 6, 0.5f, 0.8f));
                            }
                        }

                        if (EffectNumber == 3)
                        {
                            if (Anim)
                                Anim.SetTrigger("Attack2");

                            if (cameraShaker)
                                StartCoroutine(cameraShaker.Shake(0.2f, 5, 3, 0));
                        }

                        Effect.Play();

                        if (soundComponent)
                        {
                            MainSoundPlay();
                        }

                        yield return new WaitForSeconds(castingTime[EffectNumber]);

                        StopCasting(EffectNumber);

                        yield break;
                    }
                }
                else
                {
                    StopCasting(EffectNumber);

                    yield break;
                }

                yield return null;
            }
        }

        public void StopCasting(int EffectNumber)
        {
            soundComponent = null;
            soundComponentCast = null;

            if (PrefabsCast[EffectNumber])
            {
                PrefabsCast[EffectNumber].transform.parent = parentObject;
                PrefabsCast[EffectNumber].transform.localPosition = new Vector3(0, 0, 0);
            }

            if (EffectNumber == 2 && Anim)
                Anim.Play("Blend Tree");

            currNumber = EffectNumber;
            casting = false;

            if (movementInput)
                movementInput.AllowMovement();
        }

        public int targetIndex()
        {
            float[] distances = new float[screenTargets.Count];

            for (int i = 0; i < screenTargets.Count; i++)
            {
                distances[i] = Vector2.Distance(
                    Cam.WorldToScreenPoint(screenTargets[i].position),
                    new Vector2(Screen.width / 2, Screen.height / 2)
                );
            }

            float minDistance = Mathf.Min(distances);
            int index = 0;

            for (int i = 0; i < distances.Length; i++)
            {
                if (minDistance == distances[i])
                    index = i;
            }

            return index;
        }
    }
}