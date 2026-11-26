"use client";
import { useEffect } from "react";
import { aiWaker } from "@/lib/aiWaker";

/** 앱을 열면 잠든 AI 서비스를 바로 깨운다(chat-wake CW-01). 검토 화면에 갈 때쯤 깨어 있게 한다. 아무것도 그리지 않는다. */
export function AiWarmup() {
  useEffect(() => {
    void aiWaker.warm();
  }, []);
  return null;
}
