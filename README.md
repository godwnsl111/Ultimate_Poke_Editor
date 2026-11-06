# Ultimate Editor

## PKHeX 플러그인: 입수 후보 목록

`dist/UltimateEditor.dll`은 PKHeX **26.10.18**, .NET 10용 읽기 전용 플러그인입니다.
PKHeX의 `plugins` 폴더에 이 DLL 하나를 복사하고 PKHeX를 다시 실행한 뒤,
GBA FR/LG 세이브를 열고 도구 메뉴의 **Ultimate Editor — 입수 후보 목록**을 선택합니다.
플러그인은 현재 PKHeX가 선택한 게임 버전을 사용합니다. FR/LG 판별이 불확실하면 본체에서 버전을 확인해야 합니다.
기존 프로그램 설치 및 cfg.json은 자동 변경하지 않습니다.

- 현재 열린 세이브의 트레이너/TID/SID 표시
- 대상 범위 선택: **해당 지방 / 해당 게임** (기본값: 해당 지방)
- 해당 지방: 게임별 지방 도감 소속 종으로 제한. 현재 FR/LG는 관동 1~151.
- 해당 게임: 해당 버전의 전체 입수 후보. FR/LG에서는 1~386 중 기록이 있는 종만 표시.
- 이 옵션은 포획 장소의 지리적 필터가 아닙니다. 지방 도감 소속 종은 다른 지역의 입수 경로도 포함됩니다.
- 다른 게임을 위한 지방 도감은 불연속 번호 집합도 등록할 수 있도록 분리했습니다. 현재 지원 게임 자체는 FR/LG만입니다.
- 교배 제외, 게임에서 직접 선물로 주는 알은 경로 분류상 포함
- 동일 종/폼의 야생 경로 중 최저 레벨 선택 (동률은 장소 ID/방법/기록 ID 순)
- 같은 버전에 야생 미진화체가 하나라도 있으면 야생 진화형 제외
- 선물/게임코너/NPC 교환은 야생 진화형 제외 규칙을 적용하지 않음
- 고정 심볼 포획도 야생 포획으로 처리, 선물·게임코너는 별도 경로로 유지
- 선정 및 제외 근거 화면, JSON 저장

**아직 개체 생성·PokeFinder 계산·세이브 주입은 없습니다.**
직접 배포는 실제 수령 대상 검증 전까지 조회 대상에서 제외했습니다.
NPC 교환 템플릿의 레벨은 교배를 허용한 최저치일 수도 있으므로, 교배 제외 정책에 맞춘
요구 포켓몬과 최소 레벨 보정은 이후 생성 단계 전에 필요합니다.
여러 경로가 있는 선물/교환은 별도 행으로 표시되므로 행 수와 종 수는 다릅니다.
장소는 현재 내부 ID로 표시하며 게임 내 지역명 표시는 후속 작업입니다.

### 빌드와 검증

`./build-plugin.ps1`은 프로젝트 내부 SDK와 고정한 공식 PKHeX 26.10.18 소스를 사용합니다.
SDK: `tools/dotnet`, 소스: `vendor/pkhex/kwsch-PKHeX-bfe89c7`.
프로젝트 SDK 설치 중 .NET 최초 실행 동작에 의해 개발용 HTTPS 인증서가 생성되었다는 로그가 있었습니다.
빌드 스크립트는 이후 인증서 자동 생성을 비활성화합니다.
테스트는 규칙 사례와 실제 FR/LG 내장 테이블을 확인하며 원본 세이브를 읽기만 합니다.
결과 목록은 `output/catalog-FR.json`, `output/catalog-LG.json`에 기록합니다.
게임 전체 결과는 `output/catalog-FR-Game.json`, `output/catalog-LG-Game.json`에 기록합니다.
GUI는 빌드만 검증했고 실제 PKHeX에서 화면 조작 테스트는 아직 하지 않았습니다.

PKHeX 원본 소스/라이선스는 vendor 안에 보존되어 있습니다. 플러그인 코드도 GPL-3.0-or-later로 제공합니다.

## Python 보조 도구

Python 3.11 이상, 외부 Python 패키지 없이 실행합니다.
현재 단계에서는 PKHeX/PokeFinder 실행, 개체 생성, 세이브 수정이 없습니다.

명령은 `ultimate_editor` 폴더에서 실행합니다. PKHeX.exe, PokeFinder 폴더와
세이브 파일은 상위 프로젝트 폴더에서 읽고, 캐시와 출력은 이 폴더 안에 저장합니다.

## 준비된 명령

```powershell
cd "C:\Users\godwn\Pokemon Edit Project\ultimate_editor"
python poke_prepare.py inventory
python poke_prepare.py encounters --versions firered leafgreen --start 1 --end 151
python poke_prepare.py encounters --versions firered --start 25 --end 25
python poke_prepare.py encounters --versions firered --offline
python -m unittest discover -s tests -v
```

- `inventory`: 도구 존재 여부, 세이브 크기와 SHA-256 확인. 버전을 파일명으로 추정하지 않습니다.
- `encounters`: PokéAPI의 포켓몬별 encounters를 조회하고 버전별 장소/방법/레벨/확률/조건을 CSV와 JSON으로 출력합니다.
- 최초 조회는 네트워크를 사용하며 응답을 `data/cache`에 저장합니다. 이후 캐시를 재사용합니다.
- JSON에는 출현 기록이 없는 포켓몬도 `no_matching_records`로 남깁니다. 이는 입수 불가 판정이 아닙니다.
- CSV/JSON은 같은 출력 폴더에서 재실행하면 교체됩니다. 범위를 비교하려면 `--output`을 다르게 지정하세요.
- 1~151은 조회 범위일 뿐 FR/LG 전체 입수 범위라는 의미가 아닙니다.

## 확정한 입수 정책

해당 버전의 직접 포획, 게임 내 선물(알 포함), NPC 교환, 해당 버전 대상 배포를 대상으로 합니다.
교배와 진화 확장은 제외합니다. 야생 미진화체가 있으면 야생 진화형을 제외하고,
같은 종의 야생 포획은 최저 레벨 출현 경로를 선택합니다.
외부 통신교환·전송 및 이들에 의존하는 경로는 제외합니다.
목록은 선택에 따른 입수 경로의 합집합이며 한 번의 플레이에서 동시 획득을 보장하지 않습니다.

## 다음 단계

1. PKHeX 기반 세이브 판독 연동: 버전 후보, 언어, TID/SID, 박스 여유 공간. 판별 불확실성 표시.
2. 출현 데이터의 완전성 확인 및 선물/NPC 교환/배포 근거 데이터 보완.
3. NPC 교환 요구 조건과 직접 배포 수령 가능성 판정.
4. PokeFinder의 실제 연동 방식 조사: 현재는 GUI 실행 파일만 확인됨. CLI 지원은 가정하지 않음.
5. 경로별 PID/IV/성격/이로치 조합 생성. 교환/배포 고정 OT·ID와 이로치 제한 준수.
6. PKHeX 개체 적법성 검사, 빈 슬롯 추가, 별도 세이브 출력 및 재판독 검증.

PokeFinder 계산 결과만으로 전체 개체 적법성이 보장되지는 않습니다.
원본 세이브와 기존 개체는 보존하며 현재 도구에는 세이브 쓰기 기능 자체가 없습니다.

공식 API 문서: https://pokeapi.co/docs/v2#pokemon-location-areas
