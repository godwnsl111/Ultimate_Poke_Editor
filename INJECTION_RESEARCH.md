# 세이브 주입 설계 조사

조사일: 2026-11-06. PKHeX 26.10.18 로컬 공식 소스를 읽었습니다.
이 조사에서는 개체 생성·프로그램 실행·세이브 변경을 하지 않았습니다.

## 결론

플러그인에서 현재 열린 세이브에 연결할 수 있습니다.
입수 템플릿 → 개체 생성 → 형식/세이브 적용 → 검사 → 복제본 박스 기록 → 재판독 검증 → 사용자 확인 → 열린 세이브 반영 → PKHeX에서 최종 파일 저장 순서를 권장합니다.
입수 가능 경로 판정과 개체 적법성 검사는 별개로 유지합니다.

## 확인한 API

아래 경로는 vendor/pkhex/kwsch-PKHeX-bfe89c7 기준입니다.

| 기능 | 확인한 소스/API |
|---|---|
| 열린 세이브 | PKHeX.Core/Editing/ISaveFileProvider.cs: SAV |
| 입수 템플릿으로 생성 | PKHeX.Core/Legality/Encounters/Templates/Interfaces/IEncounterConvertible.cs: ConvertToPKM(ITrainerInfo, EncounterCriteria) |
| 본체의 실제 생성 흐름 | PKHeX.WinForms/Subforms/SAV_Encounters.cs: ClickView |
| 개체 형식 변환 | PKHeX.Core/PKM/Util/Conversion/EntityConverter.cs: ConvertToType |
| 트레이너 제공 | PKHeX.Core/Saves/Abstractions/SimpleTrainerInfo.cs |
| 생성 조건 | PKHeX.Core/Legality/Encounters/Generator/EncounterCriteria.cs |
| 적법성 | PKHeX.Core/Legality/LegalityAnalysis.cs: new LegalityAnalysis(pk), Valid |
| 세이브 복제 | PKHeX.Core/Saves/SaveFile.cs: Clone() |
| 박스 수/슬롯 수 | 같은 파일: BoxCount, BoxSlotCount |
| 잠금/보호 | 같은 파일: IsBoxSlotLocked, IsBoxSlotOverwriteProtected |
| 박스 읽기/쓰기 | 같은 파일: GetBoxSlotAtIndex, SetBoxSlotAtIndex |
| 주입 시 갱신 정책 | PKHeX.Core/Saves/Storage/EntityImportSettings.cs |
| 복제본 변경 반영 | SaveFile.CopyChangesFrom; SAV3에는 Small/Large/Storage를 함께 복사하는 override 존재 |
| 본체 화면 갱신 | ISaveFileProvider.ReloadSlots() |
| 직렬화/체크섬 | SaveFile.Write(), ChecksumsValid, SaveUtil.GetSaveFile |

본체 Encounter DB의 ClickView는 ConvertToPKM 후 대상 PKMType으로 변환하고 AdaptToSaveFile 및 RefreshChecksum을 적용한 뒤 편집기에 표시합니다.
그 경로는 박스 일괄 주입 자체가 아니므로 플러그인에 배치 처리와 원복 기능은 따로 필요합니다.

## 현재 코드에서 먼저 바꿀 것

현재 Route는 표시용 요약입니다. 원본 IEncounterable 참조를 보존하지 않고 순번 ID만 갖습니다.
화면용 ID를 그대로 받아 생성하려 하지 말고, 조회 시 템플릿 참조와 게임/폼/레벨/장소/조건을 함께 보존하는 실행 계획을 만듭니다.
ID 순번은 소스/버전 변경에 따라 달라질 수 있으므로 JSON을 나중에 불러올 때 재검증 없이 신뢰하면 안 됩니다.
JSON에는 표시 이름이 아니라 검증 가능한 경로 식별 정보와 사용한 PKHeX 버전도 기록합니다.

## 처리 흐름

1. 목록에 체크박스 추가. 기본은 종별 한 마리이고 경로가 여럿이면 그중 하나를 지정합니다.
2. 기본 버전/거울 버전별 트레이너를 확정합니다. 미입력 값이나 ID는 추정하지 않습니다.
3. 외부 의존 조건과 배타적 선택 정책을 확인합니다. 미검증 경로는 검증 완료 목록과 구분합니다.
4. 원본 템플릿의 ConvertToPKM에 트레이너와 조건을 전달합니다.
5. 다른 버전 출신은 출신 버전·OT·ID를 유지합니다. NPC 교환·배포의 고정값을 덮어쓰지 않습니다.
6. 대상 게임의 PKMType에 맞게 변환합니다. 본체 변환기가 지원한다는 것과 같은 게임군 내 통신교환이 가능하다는 것은 별개라서 허용 경로를 제한합니다.
7. 대상 세이브 적용 처리 후 적법성 및 사용자가 요구한 옵션을 각각 검사합니다.
8. 빈 슬롯 수를 미리 확인하고 보호된 슬롯·알·기존 개체·의심되는 비정상 데이터를 보존합니다. 종 번호가 0이라는 이유만으로 덮어쓰지 않습니다.
9. 원본 SAV.Clone()에만 먼저 추가합니다. 주입하면서 바뀐 핸들러·기술·데이터도 재판독해서 검사합니다.
10. 슬롯별 검증 후 복제본을 Write()로 메모리에 직렬화하고 다시 파싱하여 체크섬과 수량/기존 슬롯 보존을 확인합니다.
11. 미리보기: 추가 종/경로/출신/이로치/박스 좌표/실패 이유/공간 부족 표시. 검증 완료 개체만 반영합니다.
12. 실제 반영 직전에 같은 세이브와 같은 대상 슬롯인지 재확인합니다. 세이브가 교체되거나 슬롯이 바뀌면 기존 계획을 폐기합니다.
13. 확인한 계획을 열린 세이브에 반영하고 ReloadSlots. 원본 메모리 스냅샷으로 이번 일괄 작업을 취소할 수 있게 합니다.
14. 디스크 저장은 PKHeX의 일반 저장 기능으로 수행합니다. 플러그인이 원본 파일을 직접 덮어쓰지 않습니다.

CopyChangesFrom은 박스 외 데이터도 반영하므로, 미리보기 중 사용자가 다른 편집을 했다면 무조건 복사하면 안 됩니다.
세이브/상태 일치 확인 또는 반영 시 최신 세이브 복제본에 계획을 재적용하는 방식이 필요합니다.
직접 SetBoxSlotAtIndex로 쓰면 본체의 슬롯 Undo 기록에 자동 등록된다고 가정할 수 없습니다. 별도 취소 또는 SlotEditor 기록 연동이 필요합니다.

## 생성 옵션과 RNG

- 첫 단계는 닉네임 없음, 정상 입수 레벨, 임의의 정상 개체값/성격, 노력치 0, 자동 기술로 제한합니다.
- 선물/교환/배포가 고정한 값은 사용자 기본 옵션보다 우선합니다.
- 선물 알은 알 상태로 둘지 부화 개체로 만들지 UI에서 명확히 구분해야 합니다.
- 세대별 DV/IV, 성격 부재, 기술/폼/특성 규칙을 다르게 처리합니다.
- ConvertToPKM이 조건을 '시도'하는 API이므로 실패/무시된 조건을 생성 후 확인해야 합니다.
- 이로치는 생성 후 플래그/PID만 바꾸지 않습니다. 실제 생성 경로에 맞는 조합을 찾습니다.
- 일반 조건은 PKHeX의 템플릿 생성 기능을 우선 검토하고 PokeFinder는 지원하는 게임/방식의 추가 RNG 검색용으로 연결합니다.
- PokeFinder GUI 실행 파일의 자동 호출 계약은 아직 없으므로 계산 브리지 구현은 별도 작업입니다.
- 무리한 조건의 탐색에는 시간 제한과 취소가 필요합니다. 안전하게 중단하기 어려운 계산은 별도 프로세스로 격리하는 설계가 적합합니다.

## 트레이너 정보 보완

현재 거울 입력은 OT/ID/성별/언어까지입니다.
6·7세대 등에서는 국가·지역·콘솔 지역도 관련되므로, 가능하면 상대 세이브의 IRegionOrigin 정보를 보존해 사용해야 합니다.
SimpleTrainerInfo의 기본 국가/지역을 무조건 채택하면 안 됩니다. 수동 입력을 지원한다면 추가 필드와 검증이 필요합니다.
TID만으로 SID를 복원할 수는 없습니다. 표시 TID/SID 쌍 또는 상대 세이브에서 전체 ID를 읽습니다.

## 다음 구현 단위

1. 후보 한 개의 원본 템플릿을 보존하고 일반 개체를 생성/검사하여 결과만 표시하는 미리보기.
2. 파이어레드와 기라티나 등의 복제 세이브에 한 마리를 넣고 재판독하는 테스트. 실제 열린 세이브와 원본 파일은 유지.
3. 체크박스 일괄 계획, 빈 박스 할당, 취소/실패 처리, 검토 화면.
4. 사용자 확인 후 열린 세이브에 반영.
5. 세대별/입수 방식별 검증을 넓히고 마지막으로 이로치/IV 검색을 확장.

이번 결과는 소스 기반 설계 조사이며 주입이 실제로 성공했다고 검증한 단계는 아닙니다.
