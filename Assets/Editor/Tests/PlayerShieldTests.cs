using NUnit.Framework;
using UnityEngine;

namespace Asteroids.Tests
{
    [TestFixture]
    public class PlayerShieldTests
    {
        private GameObject _shieldObject;
        private PlayerShield _shield;
        private Material _templateMaterial;

        [SetUp]
        public void SetUp()
        {
            _shieldObject = new GameObject("TestShield");
            var meshRenderer = _shieldObject.AddComponent<MeshRenderer>();
            var meshFilter = _shieldObject.AddComponent<MeshFilter>();
            var sphereMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshFilter>().sharedMesh;
            meshFilter.sharedMesh = sphereMesh;

            Shader shader = Shader.Find("Particles/Standard Surface");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            _templateMaterial = new Material(shader);
            _templateMaterial.name = "TestShieldTemplate";
            _templateMaterial.SetColor("_Color", new Color(0.2f, 0.6f, 0.9f, 1f));
            if (_templateMaterial.HasProperty("_EmissionColor"))
            {
                _templateMaterial.SetColor("_EmissionColor", Color.black);
            }

            meshRenderer.sharedMaterial = _templateMaterial;
            _shield = _shieldObject.AddComponent<PlayerShield>();
            _shield._mat = _templateMaterial;
            _shield._speed = -0.5f;
            _shield.IdleAlpha = 0.5f;
            _shield.HitAlpha = 1.0f;
            _shield.FadeInDuration = 0.05f;
            _shield.FadeOutDuration = 0.2f;
            _shield.emission_idle = new Color(0f, 0.2f, 0.4f, 1f);
            _shield.emission_hit = new Color(0.5f, 1.0f, 1.5f, 1f);
            _shield.velocity_scroll_multiplier = 0.5f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_shieldObject != null)
            {
                Object.DestroyImmediate(_shieldObject);
            }
            if (_templateMaterial != null)
            {
                Object.DestroyImmediate(_templateMaterial);
            }
        }

        [Test]
        public void InitMaterialInstance_CreatesDistinctRuntimeInstance_DoesNotModifySourceAsset()
        {
            _shield.InitMaterialInstance();

            Assert.IsNotNull(_shield.MaterialInstance, "MaterialInstance should be instantiated.");
            Assert.AreNotSame(_templateMaterial, _shield.MaterialInstance, "MaterialInstance should be a clone, not the source template asset.");

            // Modify runtime instance color alpha
            _shield.MaterialInstance.SetColor("_Color", Color.black);
            Color templateColor = _templateMaterial.GetColor("_Color");
            Assert.AreEqual(0.2f, templateColor.r, 0.01f);
            Assert.AreEqual(0.6f, templateColor.g, 0.01f);
            Assert.AreEqual(0.9f, templateColor.b, 0.01f);
            Assert.AreEqual(1.0f, templateColor.a, 0.01f, "Source template material should remain pristine.");
        }

        [Test]
        public void RemembersOriginalAlbedoColor_AndInitializesAlphaToIdleAlpha()
        {
            _shield.InitMaterialInstance();

            Assert.AreEqual(0.2f, _shield.OriginalColor.r, 0.01f);
            Assert.AreEqual(0.6f, _shield.OriginalColor.g, 0.01f);
            Assert.AreEqual(0.9f, _shield.OriginalColor.b, 0.01f);

            Assert.AreEqual(0.5f, _shield.CurrentAlpha, 0.01f, "Initial alpha should match default idle alpha (0.5).");
            Color matColor = _shield.MaterialInstance.GetColor("_Color");
            Assert.AreEqual(0.5f, matColor.a, 0.01f, "Material instance alpha should match idle alpha (0.5).");
        }

        [Test]
        public void TriggerHit_TransitionsAlphaFromIdleToHitAlphaAndBack()
        {
            _shield.InitMaterialInstance();
            Assert.AreEqual(0.5f, _shield.CurrentAlpha, 0.01f);

            // Trigger hit flash
            _shield.TriggerHit(1f);
            Assert.AreEqual(PlayerShield.AlphaTransitionState.FadeIn, _shield.State);

            // Advance time through FadeIn (0.05s)
            _shield.UpdateShield(0.06f);
            Assert.AreEqual(1.0f, _shield.CurrentAlpha, 0.01f, "Alpha should reach hit alpha (1.0).");
            Assert.AreEqual(PlayerShield.AlphaTransitionState.FadeOut, _shield.State);

            // Advance time through FadeOut (0.2s)
            _shield.UpdateShield(0.25f);
            Assert.AreEqual(0.5f, _shield.CurrentAlpha, 0.01f, "Alpha should fade back down to idle alpha (0.5).");
            Assert.AreEqual(PlayerShield.AlphaTransitionState.Idle, _shield.State);
        }

        [Test]
        public void EmissionColor_InitializesToIdle_AndTransitionsToHitOnImpact()
        {
            _shield.InitMaterialInstance();

            // Initial emission color should be emission_idle
            Assert.AreEqual(_shield.emission_idle.r, _shield.CurrentEmissionColor.r, 0.01f);
            Assert.AreEqual(_shield.emission_idle.g, _shield.CurrentEmissionColor.g, 0.01f);
            Assert.AreEqual(_shield.emission_idle.b, _shield.CurrentEmissionColor.b, 0.01f);

            if (_shield.MaterialInstance.HasProperty("_EmissionColor"))
            {
                Color matEmission = _shield.MaterialInstance.GetColor("_EmissionColor");
                Assert.AreEqual(_shield.emission_idle.r, matEmission.r, 0.01f);
                Assert.AreEqual(_shield.emission_idle.g, matEmission.g, 0.01f);
                Assert.AreEqual(_shield.emission_idle.b, matEmission.b, 0.01f);
            }

            // Hit should transition emission to emission_hit
            _shield.TriggerHit(1f);
            _shield.UpdateShield(0.06f); // Advance through FadeIn

            Assert.AreEqual(_shield.emission_hit.r, _shield.CurrentEmissionColor.r, 0.01f, "Emission should peak at emission_hit.");
            Assert.AreEqual(_shield.emission_hit.g, _shield.CurrentEmissionColor.g, 0.01f);
            Assert.AreEqual(_shield.emission_hit.b, _shield.CurrentEmissionColor.b, 0.01f);

            // Advance through FadeOut
            _shield.UpdateShield(0.25f);

            Assert.AreEqual(_shield.emission_idle.r, _shield.CurrentEmissionColor.r, 0.01f, "Emission should return to emission_idle.");
            Assert.AreEqual(_shield.emission_idle.g, _shield.CurrentEmissionColor.g, 0.01f);
            Assert.AreEqual(_shield.emission_idle.b, _shield.CurrentEmissionColor.b, 0.01f);
        }

        [Test]
        public void UpdateShield_ScrollsTextureOffsetContinuousAndSeamless()
        {
            _shield.InitMaterialInstance();
            _shield._speed = -0.5f;

            _shield.UpdateShield(0.1f);
            Vector2 offset1 = _shield.MaterialInstance.GetTextureOffset("_MainTex");

            _shield.UpdateShield(0.1f);
            Vector2 offset2 = _shield.MaterialInstance.GetTextureOffset("_MainTex");

            Assert.AreNotEqual(offset1.y, offset2.y, "Texture offset Y should change over time.");
            Assert.GreaterOrEqual(offset2.y, 0f, "Texture offset should remain within [0, 1] range via repeat.");
            Assert.LessOrEqual(offset2.y, 1f, "Texture offset should remain within [0, 1] range via repeat.");
        }

        [Test]
        public void VelocityScrolling_AcceleratesTextureOffsetWithPlayerVelocity()
        {
            _shield.InitMaterialInstance();
            _shield._speed = -0.5f;
            _shield.velocity_scroll_multiplier = 1.0f;
            _shield.FadeInDuration = 0f;
            _shield.FadeOutDuration = 0f;

            // Scenario 1: Stationary (velocity = 0)
            _shield.SetSimulatedVelocity(0f);
            _shield.UpdateShield(0.01f);
            float stationarySpeed = _shield.EffectiveScrollSpeed;
            Assert.AreEqual(-0.5f, stationarySpeed, 0.01f);

            // Scenario 2: Moving (velocity = 10)
            _shield.SetSimulatedVelocity(10f);
            _shield.UpdateShield(0.01f);
            float movingSpeed = _shield.EffectiveScrollSpeed;
            Assert.AreEqual(-10.5f, movingSpeed, 0.01f);

            Assert.Greater(Mathf.Abs(movingSpeed), Mathf.Abs(stationarySpeed), "Effective scroll speed magnitude should scale up with player velocity.");
        }

        [Test]
        public void IntegratesWithPlayerTakeDamage()
        {
            var playerObj = new GameObject("PlayerTest");
            var playerComp = playerObj.AddComponent<player>();
            playerComp.shield_value = 10f;
            playerComp.shield_max_value = 10f;

            _shieldObject.transform.SetParent(playerObj.transform);
            _shield.BindPlayer(playerComp);
            _shield.InitMaterialInstance();

            // Simulate TakeDamage on player
            playerComp.TakeDamage(3f, Vector3.up, Vector3.zero);

            Assert.AreEqual(7f, playerComp.shield_value, 0.01f);
            Assert.AreEqual(PlayerShield.AlphaTransitionState.FadeIn, _shield.State, "Shield should enter FadeIn when player takes shield damage.");

            // Advance to peak
            _shield.UpdateShield(0.06f);
            Assert.AreEqual(1.0f, _shield.CurrentAlpha, 0.01f, "Alpha should hit 1.0.");

            Object.DestroyImmediate(playerObj);
        }
    }
}
