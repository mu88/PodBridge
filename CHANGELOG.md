# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

<a name="2.0.0"></a>
## [2.0.0](https://www.github.com/mu88/PodBridge/releases/tag/2.0.0) (2026-11-26)

### ✨ Features

* migrate to .NET 11 ([fbdf9f0](https://www.github.com/mu88/PodBridge/commit/fbdf9f070db41d48cf115ee9e9d31ab260fff676))
* **observability:** add Grafana Cloud dashboards ([1454831](https://www.github.com/mu88/PodBridge/commit/14548311d80e13fc2e89562fa32ac3bed0c5f486))

### 🐛 Bug Fixes

* update vulnerable .NET container runtime ([e9bd1a6](https://www.github.com/mu88/PodBridge/commit/e9bd1a6bb394bed6405462a80fe90cc6ca9ce6bd))
* **deps:** adopt shared testing publish worker fix ([f78ef98](https://www.github.com/mu88/PodBridge/commit/f78ef98b2bce1a8cb4c90fa2911a3ec86df3c677))
* **feeds:** use AntennaPod HTTPS deeplink ([1783d7a](https://www.github.com/mu88/PodBridge/commit/1783d7abef07eeeab2600fbc189aa4088940abc9))
* **security:** patch runtime and test dependency ([e1dfa3d](https://www.github.com/mu88/PodBridge/commit/e1dfa3d609b94125fb80dc74fcd2bdb611b92ded))
* **testing:** derive Playwright server image tag from resolved NuGet version ([9e60c82](https://www.github.com/mu88/PodBridge/commit/9e60c823a8c9012961e7fbac5590e29f5390ee0a))

### ♻️ Refactors

* remove completed RefreshInterval and legacy-podcast migration shims ([2efc748](https://www.github.com/mu88/PodBridge/commit/2efc748ca34934b869c142ca8d3f10396f843914))
* **ci:** use centralized pin guard ([426a17b](https://www.github.com/mu88/PodBridge/commit/426a17bd502ad3bca8c954c995a0242700d79986))
* **tests:** adopt mu88.Shared.Testing for System test infrastructure ([85c1846](https://www.github.com/mu88/PodBridge/commit/85c1846f2efcb293f487950a414510a994bf6d31))
* **tests:** source testcontainers images from structured JSON for Renovate tracking ([afe9d5f](https://www.github.com/mu88/PodBridge/commit/afe9d5faec40d7243f42d086ba1e527cc13ddacc))

### ✅ Tests

* add Playwright UI E2E system tests with parallelized Docker startup ([14ef72f](https://www.github.com/mu88/PodBridge/commit/14ef72f144751ba903cc7c429c01d9ce262f416e))
* close mutation testing gaps ([4d6b037](https://www.github.com/mu88/PodBridge/commit/4d6b037a1fea5a999f642c33e82f29017f59564b))
* **mutation:** exclude System tests from Stryker mutation runs ([234ba6d](https://www.github.com/mu88/PodBridge/commit/234ba6dd1423665b8fd2ff69c8c178a48d3dd29a))

### 🔧 Chores

* **deps:** update all .net ([f53befd](https://www.github.com/mu88/PodBridge/commit/f53befdf374c5b0fc78e298af1efae250b186ade))
* **deps:** update all .net ([89baad5](https://www.github.com/mu88/PodBridge/commit/89baad5e839a16974012ea5dac724bfc93756e32))
* **deps:** update all dependencies ([2e151fe](https://www.github.com/mu88/PodBridge/commit/2e151fe62fe78dd5ab9c0395090d63b7648c2c56))
* **deps:** update all dependencies ([21657fd](https://www.github.com/mu88/PodBridge/commit/21657fd353600627192187f9ba4d23e83bcef612))
* **deps:** update all dependencies ([e64d3b8](https://www.github.com/mu88/PodBridge/commit/e64d3b8c9628b99b422cd6d3e55a3aebc8e84fc2))
* **deps:** update all dependencies ([84543b9](https://www.github.com/mu88/PodBridge/commit/84543b9a7c235b6229ece1ccb45d2c09538247a4))
* **deps:** update dependency dotnet-coverage to v18.12.0 ([a899943](https://www.github.com/mu88/PodBridge/commit/a899943365ac4d9a70bf81a458823aa97d7e8650))
* **deps:** update mu88/common digest to 2edcdac ([8cc05aa](https://www.github.com/mu88/PodBridge/commit/8cc05aadc8acdaefd014dfed0b16b24c9113fef0))
* **deps:** update mu88/common digest to 309b9cb ([cbcd8e2](https://www.github.com/mu88/PodBridge/commit/cbcd8e22b90db23a21ac1b374ed7c45846457deb))
* **deps:** update mu88/common digest to 74c30dc ([44c8190](https://www.github.com/mu88/PodBridge/commit/44c8190148de5c7a7725fd5d50616a66798865e5))
* **deps:** update mu88/common digest to b59f07c ([409e74d](https://www.github.com/mu88/PodBridge/commit/409e74d991844d34d86508d2136a46b50f080ad0))
* **feeds:** document intentional AntennaPod URI ([b3ea494](https://www.github.com/mu88/PodBridge/commit/b3ea4949daedf225f98a4ee40ff095d7ffa70bac))

### Breaking Changes

* migrate to .NET 11 ([fbdf9f0](https://www.github.com/mu88/PodBridge/commit/fbdf9f070db41d48cf115ee9e9d31ab260fff676))

<a name="1.1.0"></a>
## [1.1.0](https://www.github.com/mu88/PodBridge/releases/tag/1.1.0) (2026-10-01)

### ✨ Features

* **podcasts:** add self-service podcast search/add and clean up architecture ([29adcf8](https://www.github.com/mu88/PodBridge/commit/29adcf841d8530c7ef4ecd95c12b7da4d45363f1))

### 🔧 Chores

* **deps:** update all .net ([a38115d](https://www.github.com/mu88/PodBridge/commit/a38115d58ff51e840fd51ff5853ac1cf012f68c2))
* **deps:** update all .net ([17d106e](https://www.github.com/mu88/PodBridge/commit/17d106ef05c568f7e28744b0902e2f986cc30586))
* **deps:** update all dependencies ([6966898](https://www.github.com/mu88/PodBridge/commit/69668980590e79fffa63755b2f71c10198f6dc72))
* **deps:** update all dependencies ([465ac1d](https://www.github.com/mu88/PodBridge/commit/465ac1de224ecb796c056802a899e657f1c1c0da))
* **deps:** update all dependencies ([fe431f4](https://www.github.com/mu88/PodBridge/commit/fe431f49ae12cbc1b1a26ebe95b9d048063a5e17))
* **deps:** update all dependencies ([03fe171](https://www.github.com/mu88/PodBridge/commit/03fe1715186f8adf60923488d35d5a2bfc20f135))
* **deps:** update dependency scalar.aspnetcore to 2.17.3 ([a8a85a3](https://www.github.com/mu88/PodBridge/commit/a8a85a39bf9e1cb424b3095f5c8f0c6df8f1e4e8))
* **deps:** update mu88/common digest to 03ce929 ([5ad959e](https://www.github.com/mu88/PodBridge/commit/5ad959eef2f8ace015c13814658b0d03e8dd6ff1))
* **deps:** update mu88/common digest to 0d04e69 ([2865148](https://www.github.com/mu88/PodBridge/commit/2865148b52532fed0788a682bcb993ba949a4c8f))
* **deps:** update mu88/common digest to 349db23 ([c003290](https://www.github.com/mu88/PodBridge/commit/c003290618f371f84b0a71d9531df37a5bae5154))
* **deps:** update mu88/common digest to 5d4855b ([4e9a58e](https://www.github.com/mu88/PodBridge/commit/4e9a58edace7afc8d1460d4c3bcceecf11f4cd71))
* **deps:** update mu88/common digest to 88212c6 ([6e7cd80](https://www.github.com/mu88/PodBridge/commit/6e7cd801bda7271e745226d332ae2c5a8a3c4b39))
* **deps:** update mu88/common digest to fedc5a3 ([f8bb934](https://www.github.com/mu88/PodBridge/commit/f8bb934694f69880bcd2f08cff45985e79a97a67))

<a name="1.0.1"></a>
## [1.0.1](https://www.github.com/mu88/PodBridge/releases/tag/1.0.1) (2026-09-01)

### 🐛 Bug Fixes

* provide Podcast image URL according to RSS/itunes spec ([b834be7](https://www.github.com/mu88/PodBridge/commit/b834be7fb37b886775534976b59b793ddf69cde0))

### ✅ Tests

* strengthen mutation-testing coverage for options defaults, refresh worker telemetry, and version parsing ([6901a82](https://www.github.com/mu88/PodBridge/commit/6901a82e1f2ca67175a8957542ba61540c63fecc))

<a name="1.0.0"></a>
## [1.0.0](https://www.github.com/mu88/PodBridge/releases/tag/1.0.0) (2026-09-01)

### ✨ Features

* adopt mu88.Shared 8.3.0 HttpClient log noise suppression ([d798dd9](https://www.github.com/mu88/PodBridge/commit/d798dd978d555d083159303bd60856476274f1e3))
* display app version in UI footer and align OTel service.version ([14f29bb](https://www.github.com/mu88/PodBridge/commit/14f29bb3ee534cacbfcf73016cca794713df0df9))
* **auth:** replace Basic Auth with Blazor login and add Scalar API docs ([c26e63d](https://www.github.com/mu88/PodBridge/commit/c26e63d66f284d9481da581029cd639bd3ccc2a1))
* **renovate:** add config ([b463049](https://www.github.com/mu88/PodBridge/commit/b463049f13924570d2e5f9b3c07f41d725c58fa4))

### 🐛 Bug Fixes

* **api:** load external config file only when its path is explicitly configured ([ae789a4](https://www.github.com/mu88/PodBridge/commit/ae789a48b737445b4f0332a081061487437667c7))
* **auth:** only rate-limit login POST attempts, not GET page views ([4846307](https://www.github.com/mu88/PodBridge/commit/4846307229fcc08931bc7054a876e237b383c05b))
* **deps:** bump Microsoft.AspNetCore.OpenApi to 10.0.11 to resolve NU1903 ([080ef9a](https://www.github.com/mu88/PodBridge/commit/080ef9a91c2305a66d28493208b639573a36e364))
* **ui:** label UTC timestamps explicitly instead of misrepresenting them as local time ([5007394](https://www.github.com/mu88/PodBridge/commit/5007394300fa2d89d325c220eafb426de3c28f86))

### ♻️ Refactors

* remove unused PathBase reverse-proxy override (YAGNI) ([619145e](https://www.github.com/mu88/PodBridge/commit/619145e5bd114a246480525911016ce3e553b119))

### 🔧 Chores

* adopt mu88.Shared 8.2.0's native health-check OTel support ([91fb3d7](https://www.github.com/mu88/PodBridge/commit/91fb3d750f1f8675b17eab8e49eeccf1a4571b10))
* reduce OTel noise and surface health-check metrics ([135d78d](https://www.github.com/mu88/PodBridge/commit/135d78de53e44f7ddce6712319e84c4ab8ed2bf1))
* resolve Sonar issues ([74d35d5](https://www.github.com/mu88/PodBridge/commit/74d35d59c2aef31f3635b76c738cbe7cdf495b54))
* **deps:** update all .net ([9592a32](https://www.github.com/mu88/PodBridge/commit/9592a322ae599b36b54182232cde41a3dae87ab3))
* **deps:** update all dependencies ([a708759](https://www.github.com/mu88/PodBridge/commit/a7087597f333836f0fb33ac47702be3d8ce3be5e))

<a name="0.1.0"></a>
## [0.1.0](https://www.github.com/mu88/PodBridge/releases/tag/0.1.0) (2026-08-31)

### ✨ Features

* initialize repo ([61d46cf](https://www.github.com/mu88/PodBridge/commit/61d46cfccbcddcc280aef745fec3711554bb0a22))
* replace Azure Container Apps deployment with hostim.dev and hash Basic Auth credentials ([d5d7a40](https://www.github.com/mu88/PodBridge/commit/d5d7a405e16d694685a03b3b4f00f574cc667d8d))
* support external JSON config file for Podcasts and Auth settings ([a8a478d](https://www.github.com/mu88/PodBridge/commit/a8a478dc4b525b43c723e3ebea989d9d2a3f3c02))

### 🐛 Bug Fixes

* repair System Tests Docker build and resolve all Sonar findings ([60068e9](https://www.github.com/mu88/PodBridge/commit/60068e9dde61d9e39b3206dfff67108ec7c82c51))
* resolve first CI/CD run failures (docker login, secret expansion, test filtering) ([fbc57f0](https://www.github.com/mu88/PodBridge/commit/fbc57f041e01963829759851dea23de7b17acff1))
* suppress noisy Basic auth log messages for unauthenticated health checks ([7505c63](https://www.github.com/mu88/PodBridge/commit/7505c631163fcd5744768ff6babb0060800219aa))

