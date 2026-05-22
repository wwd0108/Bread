mergeInto(LibraryManager.library, {

  // C# -> JavaScript: 빵 감지 이벤트 브라우저 전달
  TriggerOnBreadDetected: function (breadIdPtr) {
    var breadId = UTF8ToString(breadIdPtr);
    console.log("[WebGL Bridge] C# sent OnBreadDetected: " + breadId);
    
    if (window.OnBreadDetected) {
      window.OnBreadDetected(breadId);
    } else {
      console.warn("[WebGL Bridge] window.OnBreadDetected is not defined in HTML.");
    }
  },

  // C# -> JavaScript: 3회 이상 매칭 실패 정보 브라우저 전달
  TriggerOnTrackingFailed: function () {
    console.log("[WebGL Bridge] C# sent OnTrackingFailed");
    
    if (window.OnTrackingFailed) {
      window.OnTrackingFailed();
    } else {
      console.warn("[WebGL Bridge] window.OnTrackingFailed is not defined in HTML.");
    }
  }

});
