# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

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

