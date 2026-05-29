const { spawn } = require('child_process');

console.log('🚀 고안정성 Web AR HTTPS 개발 터널을 기동하는 중...');

// 1. http-server 실행 (포트 8080, 루트 디렉토리 서빙)
const httpServer = spawn('npx.cmd', ['-y', 'http-server', '-p', '8080', '-c-1'], {
    shell: true,
    cwd: __dirname + '/..'
});

httpServer.stdout.on('data', (data) => {
    const output = data.toString();
    if (output.includes('Available on:')) {
        console.log('💻 로컬 웹 서버가 포트 8080에서 가동되었습니다.');
    }
});

httpServer.stderr.on('data', (data) => {
    console.error(`[서버 오류]: ${data}`);
});

// 2. SSH (Pinggy) 기반 초간편 터널 시도 (IP 인증화면이 없어 접속이 매우 원활함)
function startPinggyTunnel() {
    console.log('🌐 Pinggy 터널 연결을 시도합니다 (보안 입력 화면 없음)...');
    
    // StrictHostKeyChecking=no 옵션을 주어 SSH 키 확인 팝업을 스킵합니다.
    const tunnel = spawn('ssh', [
        '-o', 'StrictHostKeyChecking=no',
        '-p', '443',
        '-R', '0:localhost:8080',
        'qr@pinggy.io'
    ], { shell: true });

    let hasUrl = false;

    tunnel.stdout.on('data', (data) => {
        const output = data.toString();
        
        // Pinggy에서 출력하는 HTTPS 링크 감지
        const httpsRegex = /(https:\/\/[a-zA-Z0-9-]+\.pinggy\.link)/g;
        const matches = output.match(httpsRegex);
        
        if (matches && matches.length > 0 && !hasUrl) {
            hasUrl = true;
            const url = matches[0];
            console.log('\n======================================================');
            console.log('🎉 초간편 HTTPS 터널 연결 성공!');
            console.log(`📱 모바일 접속 주소: ${url}/scratch/preview.html`);
            console.log('======================================================\n');
            console.log('위의 링크를 클릭하시거나 QR 코드를 스캔해 바로 입장하세요!');
        }
        
        // 터미널에 QR 코드나 핑기 상태창 출력 유지
        if (output.trim()) {
            console.log(output);
        }
    });

    tunnel.stderr.on('data', (data) => {
        const errOutput = data.toString();
        // SSH 실패 시 로컬터널로 자동 전환
        if (errOutput.includes('Host key verification failed') || errOutput.includes('ssh: connect')) {
            console.warn('⚠️ SSH 터널 실패. 로컬터널(localtunnel)로 우회 전환합니다...');
            tunnel.kill();
            startLocalTunnelFallback();
        }
    });

    tunnel.on('close', (code) => {
        if (!hasUrl) {
            console.warn('⚠️ Pinggy 연결이 종료되었습니다. 로컬터널로 전환합니다.');
            startLocalTunnelFallback();
        }
    });
}

// 3. 로컬터널 재시도 루프 (SSH가 실패할 경우의 예비용)
function startLocalTunnelFallback() {
    console.log('🌐 예비용 로컬터널(localtunnel)을 연결하는 중...');
    
    const tunnel = spawn('npx.cmd', ['-y', 'localtunnel', '--port', '8080'], {
        shell: true
    });

    tunnel.stdout.on('data', (data) => {
        const output = data.toString().trim();
        if (output.includes('your url is:')) {
            const url = output.split('your url is:')[1].trim();
            console.log('\n======================================================');
            console.log('🎉 예비용 로컬터널 연결 완료!');
            console.log(`📱 모바일 접속 주소: ${url}/scratch/preview.html`);
            console.log('======================================================\n');
        }
    });

    tunnel.on('close', (code) => {
        console.warn(`⚠️ 로컬터널이 종료되었습니다 (Exit code: ${code}). 5초 후 재연결을 시도합니다...`);
        setTimeout(startLocalTunnelFallback, 5000);
    });
}

// 3초 대기 후 터널 실행
setTimeout(startPinggyTunnel, 3000);

process.on('SIGINT', () => {
    console.log('\n🛑 서버 및 터널을 안전하게 종료합니다.');
    httpServer.kill();
    process.exit();
});
