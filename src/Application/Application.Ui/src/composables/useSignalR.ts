import { HubConnectionBuilder, HubConnection } from "@microsoft/signalr";
async function startConnection(connection?: HubConnection) {
  if (!connection) return;
  await connection.start();
}
async function stopConnection(connection?: HubConnection) {
  if (!connection) return;
  await connection.stop();
}

export async function useSignalR(
  endpoint: string = "http://localhost:5000/game"
) {
  const connection = new HubConnectionBuilder().withUrl(endpoint).build();
  const { on, state, connectionId, send, invoke } = connection;
  await startConnection(connection);
  return {
    on,
    connectionId,
    send,
    invoke,
    connectionStatus: state,
    stopConnection: async () => stopConnection(connection),
  };
}
export default useSignalR;
