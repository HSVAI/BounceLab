# Bounce Lab

공이 자동으로 튀는 정밀 플랫폼 게임과 모바일 맵 에디터를 하나의 APK에 담은 프로젝트입니다.

## 플레이

- 화면 아래의 왼쪽/오른쪽 버튼을 누르고 있는 동안 공을 조종합니다.
- 일반 블록은 자동으로 튀고, 노란 스프링은 더 높이 튑니다.
- 빨간 가시에 닿으면 시작점에서 즉시 재도전합니다.
- 초록색 GO 타일에 닿으면 클리어입니다.

## 음악과 효과음

- 오리지널 8마디 칩튠 `Neon Rebound`가 126 BPM으로 반복 재생됩니다.
- 공 튕김, 스프링, 추락, 클리어, 버튼과 맵 페인팅에 각각 합성 효과음이 적용됩니다.
- 홈 화면에서 BGM과 SFX를 따로 켜고 끌 수 있으며 설정은 기기에 저장됩니다.
- 모든 소리는 프로젝트 코드가 직접 합성하므로 외부 음원이나 별도 저작권 소재를 사용하지 않습니다.

## 맵 제작과 공유

- `MAP MAKER`에서 10 × 16 타일 맵을 터치로 편집합니다.
- 블록, 가시, 도착점, 스프링, 시작점을 배치할 수 있습니다.
- `TEST`로 업로드 전에 직접 플레이합니다.
- `UPLOAD`는 익명 제작자 이름과 맵을 Bounce Lab 전용 서버에 게시합니다.
- `COMMUNITY MAPS`에서 다른 사람이 만든 최신 맵을 받아 플레이합니다.
- 서버 주소는 GitHub Pages의 `api.json`에서 찾으므로 HTTPS 터널이 바뀌어도 앱 재빌드가 필요 없습니다.

## 빌드

Unity 2022.3.62f3 Personal과 Android Build Support를 사용합니다.

```bash
./build.sh test
./build.sh linux
./build.sh android
```

Android 산출물은 `Builds/Android/BounceLab.apk`입니다. Android 6.0 이상, ARM64, OpenGL ES 3을 대상으로 합니다.

## 백엔드

`Backend/`는 Flask, Waitress, SQLite로 구성된 독립 서비스입니다. 맵 스키마 검증, 요청 크기 제한,
IP 해시 기반 업로드 속도 제한, 기본 맵 3개, 조회 및 완주 카운트를 제공합니다.

```bash
cd Backend
.venv/bin/python -m unittest discover -s tests -v
```

백엔드는 `bouncelab-maps.service`, 외부 HTTPS 연결은 `bouncelab-maps-tunnel.service`로 실행됩니다.
계정 없는 Quick Tunnel은 테스트 배포용이므로 정식 서비스에서는 고정 도메인의 named tunnel로 교체해야 합니다.
