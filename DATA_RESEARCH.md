# FR/LG 입수 데이터 조사

후속 구현: 확정된 최신 정책에 따라 교배 및 진화 확장은 하지 않습니다.
읽기 전용 C# 플러그인과 실제 FR/LG 데이터 테스트를 `plugin`에 구현했습니다.
이 문서 아래의 초기 구현 순서보다 README의 현재 상태가 우선합니다.

조사일: 2026-11-06. 로컬 실행 파일은 실행하지 않았고 세이브는 변경하지 않았습니다.
아래는 공개 저장소 master 소스 조사 결과입니다. 설치된 실행 파일과 같은 버전이라는 보장은 없으며 실제 플러그인 구현 전 버전을 고정해야 합니다.

## 결론

대규모 웹 크롤링을 먼저 만들 필요는 없습니다. PKHeX.Core를 입수 템플릿의 주 공급원으로 삼고 PokeFinder는 RNG 계산에 활용하는 설계가 적합합니다.
PokéAPI 도구는 보조 조회/교차 확인용으로 유지합니다.

## 확인한 소스

- PKHeX FR/LG 데이터: https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Legality/Encounters/Data/Gen3/Encounters3FRLG.cs
  - FR/LG 야생 리소스를 각각 로드합니다.
  - 고정 템플릿에 스타팅, 화석 복원, 선물, 고정 심볼, 게임코너가 있습니다.
  - NPC 교환 템플릿에 PID, IV, ID 등 고정 조건이 있습니다.
  - internal 클래스이므로 플러그인에서 테이블을 직접 참조하는 설계를 전제하지 않습니다.
- 공개 후보 조회: https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Legality/Encounters/Generator/ByGeneration/EncounterGenerator3.cs
  - GetPossible은 진화 후보와 버전, 범주를 받아 입수 템플릿을 열거합니다.
  - 알 생성 가능성 검사는 로컬 부모 확보 가능성을 증명하지 않습니다.
- 후보 열거 구현: https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Legality/Encounters/Generator/Possible/EncounterPossible3.cs
  - Egg/Mystery/Trade/Static/Slot 범주와 버전별 분기가 있습니다.
- 배포 데이터: https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/Legality/Encounters/Data/Gen3/EncountersWC3.cs
  - 배포별 생성 방식, 언어, OT/ID, 이로치 제한 등을 표현합니다.
  - 출신 버전 필드를 실제 배포 수령 가능 게임 목록으로 단정하면 안 됩니다.
  - FR 직접 배포 허용 목록은 수령 대상/언어/지역의 별도 근거를 확인해야 합니다.
- PokeFinder 출현 데이터 로더: https://github.com/Admiral-Fish/PokeFinder/blob/master/Core/Gen3/Encounters3.cpp
  - FireRed/LeafGreen별 압축 리소스에서 풀숲/파도타기/바위깨기/낚시 슬롯과 레벨을 읽습니다.
  - PKHeX와 장소 ID 체계가 같다는 가정은 금지합니다. 매핑이 필요합니다.
- PokeFinder 야생 생성기: https://github.com/Admiral-Fish/PokeFinder/blob/master/Core/Gen3/Generators/WildGenerator3.cpp
  - 지역/슬롯과 프로필, 생성 방식, 필터를 받는 C++ 계산 코드가 있습니다.
  - 실행 파일의 외부 호출 API/CLI 지원은 아직 확인하지 않았습니다.

## 별도로 필요한 정책 데이터

1. 대상 번호 범위: FR/LG에서는 사용자 정책상 관동 1~151. 이것만으로 입수 가능 판정을 하지 않습니다.
2. NPC 교환에 내줄 종: 일부 소스 주석에 있어도 구조화된 공개 필드라고 가정하지 않습니다. 요구 종과 로컬 확보 가능성을 검증합니다.
3. 진화: 외부 교환 필요 여부, 아이템 등 조건을 해당 버전에서 충족할 수 있어야 합니다.
4. 교배: 초기 확보 종에서 부모/성별/메타몽 등 실제 조합을 계산합니다. 결과 범위 필터는 중간 부모 확보 계산과 분리합니다.
5. 배포/이벤트: 직접 개체 배포와 티켓 배포 후 게임 내 포획을 구분합니다. 불확실한 항목은 자동 포함하지 않고 미확정으로 남깁니다.
6. 선택형 경로는 합집합으로 다룹니다. 현재 스토리 진행에 따른 접근 가능성은 별도 모드로 두고 초기에는 게임 전체 범위를 다룹니다.

## 구현 순서

1. 설치 PKHeX 버전과 일치하는 소스/참조 어셈블리 및 .NET SDK 확인.
2. C# 플러그인에서 현재 세이브의 버전/트레이너 정보를 읽어 표시하는 최소 기능.
3. 공개 후보 조회를 감싸는 어댑터로 FR의 Slot/Static/Trade를 읽기 전용 보고서로 출력.
4. 표본 검증: 스타팅, 화석, 게임코너, NPC 교환, FR/LG 한정종, 야생 장소/방법/레벨.
5. 로컬 입수 가능성 규칙을 적용하고 진화/교배를 확장. 배포는 검증된 허용 항목만 합침.
6. PokeFinder 계산 브리지 구현 후 개체 생성 및 PKHeX 적법성 검증.
7. 검토 화면 후 현재 열린 세이브의 빈 슬롯에 반영. 최종 디스크 저장은 PKHeX에서 수행.

## 아직 하지 않은 것

실제 테이블 전체 추출, 종 수 확정, 설치 바이너리 API 호환 확인, 플러그인 빌드,
PokeFinder 계산 호출, 개체 생성, 세이브 주입은 하지 않았습니다.
