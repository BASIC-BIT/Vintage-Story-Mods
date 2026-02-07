import type { Plugin } from "@opencode-ai/plugin"

// Auto-continue loop driver.
//
// Goal: treat agent responses as reports and keep going automatically until an objective stop token is emitted.
//
// This plugin is intentionally opt-in via environment variables to avoid surprising behavior in normal interactive use.

let iterationsBySessionId: Record<string, number> = {}
let lastProcessedAssistantMessageIdBySessionId: Record<string, string> = {}

function isTruthy(v: string | undefined) {
  const s = (v || "").trim().toLowerCase()
  return s === "1" || s === "true" || s === "yes"
}

function getSessionIdFromEventProps(props: any): string | null {
  const v = props?.id || props?.sessionId || props?.session?.id
  return typeof v === "string" && v.trim() ? v.trim() : null
}

function extractText(parts: any[]): string {
  if (!Array.isArray(parts)) return ""
  return parts
    .map((p) => {
      if (p?.type === "text" && typeof p.text === "string") return p.text
      return ""
    })
    .join("")
}

export const AutoContinue: Plugin = async ({ client }) => {
  const enabled = isTruthy(process.env.OPENCODE_AUTO_CONTINUE)
  const maxIterations = Math.max(1, Math.min(200, Math.floor(Number(process.env.OPENCODE_AUTO_CONTINUE_MAX || "25"))))
  const promise = (process.env.OPENCODE_AUTO_CONTINUE_PROMISE || "<promise>COMPLETE</promise>").trim()
  const forcedSessionId = (process.env.OPENCODE_AUTO_CONTINUE_SESSION_ID || "").trim()
  const continueMessage = (process.env.OPENCODE_AUTO_CONTINUE_MESSAGE ||
    "Thanks for the report. Continue with the next highest-value step. Use the sticky todo list as source of truth. If human input is required, ask exactly one concrete question using the question tool and then stop.").trim()

  return {
    event: async ({ event }: any) => {
      if (!enabled) return
      if (event?.type !== "session.idle") return

      const sessionId = forcedSessionId || getSessionIdFromEventProps(event?.properties)
      if (!sessionId) return

      const iters = iterationsBySessionId[sessionId] ?? 0
      if (iters >= maxIterations) {
        await client.app.log({
          body: {
            service: "auto-continue",
            level: "warn",
            message: `Auto-continue reached max iterations (${maxIterations}) for session ${sessionId}`,
          },
        })
        return
      }

      // Fetch messages and inspect the latest assistant output.
      const messages = await client.session.messages({ path: { id: sessionId } })
      const items = messages.data
      const last = Array.isArray(items) && items.length ? items[items.length - 1] : null
      const lastInfo = last?.info
      const lastParts = last?.parts

      const lastMsgId = typeof lastInfo?.id === "string" ? lastInfo.id : ""
      const lastRole = lastInfo?.role
      const lastText = extractText(lastParts)

      if (lastRole !== "assistant") return
      if (!lastMsgId) return

      // Don't process the same assistant message repeatedly.
      if (lastProcessedAssistantMessageIdBySessionId[sessionId] === lastMsgId) return
      lastProcessedAssistantMessageIdBySessionId[sessionId] = lastMsgId

      // Stop condition: completion promise present.
      if (promise && lastText.includes(promise)) {
        await client.app.log({
          body: {
            service: "auto-continue",
            level: "info",
            message: `Auto-continue stop token observed for session ${sessionId}`,
          },
        })
        return
      }

      iterationsBySessionId[sessionId] = iters + 1

      await client.app.log({
        body: {
          service: "auto-continue",
          level: "info",
          message: `Auto-continue iteration ${iters + 1}/${maxIterations} for session ${sessionId}`,
        },
      })

      // Trigger the next turn.
      await client.session.prompt({
        path: { id: sessionId },
        body: {
          parts: [{ type: "text", text: continueMessage }],
        },
      })
    },
  }
}
