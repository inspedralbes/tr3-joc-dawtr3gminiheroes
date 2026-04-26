const express = require('express');
const http = require('http');
const sqlite3 = require('sqlite3').verbose();
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const cors = require('cors');
const { WebSocketServer } = require('ws');

const app = express();
const PORT = Number(process.env.PORT || 3000);
const SECRET_KEY = process.env.JWT_SECRET || 'miniheroes_super_secret_key';
const MAX_PLAYERS_PER_ROOM = 2;
const HEARTBEAT_INTERVAL_MS = 25000;

app.use(cors({ origin: true }));
app.use(express.json());

const db = new sqlite3.Database('./database.sqlite', (err) => {
    if (err) {
        console.error('Error connecting to database:', err.message);
        return;
    }

    console.log('Connected to SQLite database.');
    db.run(`CREATE TABLE IF NOT EXISTS users (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        username TEXT UNIQUE,
        password TEXT
    )`, () => {
        db.run('ALTER TABLE users ADD COLUMN experience INTEGER DEFAULT 0', () => {});
        db.run('ALTER TABLE users ADD COLUMN grunts_killed INTEGER DEFAULT 0', () => {});
        db.run('ALTER TABLE users ADD COLUMN level INTEGER DEFAULT 1', () => {});
        db.run('ALTER TABLE users ADD COLUMN stat_points INTEGER DEFAULT 0', () => {});
        db.run('ALTER TABLE users ADD COLUMN speed INTEGER DEFAULT 5', () => {});
        db.run('ALTER TABLE users ADD COLUMN max_health INTEGER DEFAULT 10', () => {});
        db.run('ALTER TABLE users ADD COLUMN attack INTEGER DEFAULT 1', () => {});
    });
});

app.post('/api/register', async (req, res) => {
    const { username, password } = req.body;
    if (!username || !password) {
        return res.status(400).json({ error: 'Falta username o password' });
    }

    try {
        const hashedPassword = await bcrypt.hash(password, 10);
        const sql = 'INSERT INTO users (username, password) VALUES (?, ?)';
        db.run(sql, [username, hashedPassword], function onInserted(err) {
            if (err) {
                if (String(err.message || '').includes('UNIQUE')) {
                    return res.status(400).json({ error: 'El usuario ya existe' });
                }
                return res.status(500).json({ error: 'Error en la base de datos' });
            }

            return res.status(201).json({
                message: 'Usuario registrado exitosamente',
                userId: this.lastID
            });
        });
    } catch (_error) {
        return res.status(500).json({ error: 'Error interno del servidor' });
    }
});

app.post('/api/login', (req, res) => {
    const { username, password } = req.body;
    if (!username || !password) {
        return res.status(400).json({ error: 'Falta username o password' });
    }

    const sql = 'SELECT * FROM users WHERE username = ?';
    db.get(sql, [username], async (err, user) => {
        if (err) {
            return res.status(500).json({ error: 'Error en la base de datos' });
        }

        if (!user) {
            return res.status(401).json({ error: 'Credenciales inválidas' });
        }

        const isMatch = await bcrypt.compare(password, user.password);
        if (!isMatch) {
            return res.status(401).json({ error: 'Credenciales inválidas' });
        }

        const token = jwt.sign({ id: user.id, username: user.username }, SECRET_KEY, { expiresIn: '24h' });
        return res.json({ message: 'Login exitoso', token, username: user.username });
    });
});

const verifyToken = (req, res, next) => {
    const authHeader = req.headers.authorization;
    const token = authHeader && authHeader.split(' ')[1];

    if (!token) {
        return res.status(403).json({ error: 'Se requiere un token de sesión para esto' });
    }

    return jwt.verify(token, SECRET_KEY, (err, decoded) => {
        if (err) {
            return res.status(401).json({ error: 'Token de sesión inválido o expirado' });
        }

        req.user = decoded;
        return next();
    });
};

app.get('/api/me', verifyToken, (req, res) => {
    res.json({ message: 'Sesión confirmada', user: req.user });
});

app.get('/api/stats', verifyToken, (req, res) => {
    db.get(
        'SELECT experience, grunts_killed, level, stat_points, speed, max_health, attack FROM users WHERE id = ?',
        [req.user.id],
        (err, row) => {
            if (err) {
                return res.status(500).json({ error: 'Error en la base de datos' });
            }

            return res.json(
                row || {
                    experience: 0,
                    grunts_killed: 0,
                    level: 1,
                    stat_points: 0,
                    speed: 5,
                    max_health: 10,
                    attack: 1
                }
            );
        }
    );
});

app.post('/api/stats', verifyToken, (req, res) => {
    const { experience, grunts_killed, level, stat_points, speed, max_health, attack } = req.body;
    db.run(
        'UPDATE users SET experience = ?, grunts_killed = ?, level = ?, stat_points = ?, speed = ?, max_health = ?, attack = ? WHERE id = ?',
        [
            experience || 0,
            grunts_killed || 0,
            level || 1,
            stat_points || 0,
            speed || 5,
            max_health || 10,
            attack || 1,
            req.user.id
        ],
        (err) => {
            if (err) {
                return res.status(500).json({ error: 'Error en la base de datos al guardar' });
            }

            return res.json({
                message: 'Estadísticas guardadas con éxito',
                experience,
                grunts_killed,
                level,
                stat_points,
                speed,
                max_health,
                attack
            });
        }
    );
});

const rooms = new Map();

function normalizeRoomId(rawRoomId) {
    return String(rawRoomId || '').trim().toUpperCase();
}

function createClientId() {
    return Math.random().toString(36).slice(2, 11);
}

function safeSend(ws, payload) {
    if (!ws || ws.readyState !== 1) {
        return;
    }

    ws.send(JSON.stringify(payload));
}

function getOrCreateRoom(roomId) {
    let room = rooms.get(roomId);
    if (!room) {
        room = {
            id: roomId,
            hostId: null,
            started: false,
            clients: new Map()
        };
        rooms.set(roomId, room);
    }
    return room;
}

function disposeRoomIfEmpty(room) {
    if (!room || room.clients.size > 0) {
        return;
    }

    rooms.delete(room.id);
}

function assignSlot(room) {
    const used = new Set();
    for (const client of room.clients.values()) {
        if (client.slot === 1 || client.slot === 2) {
            used.add(client.slot);
        }
    }

    if (!used.has(1)) {
        return 1;
    }

    if (!used.has(2)) {
        return 2;
    }

    return -1;
}

function lobbyPlayers(room) {
    const players = [];
    for (const client of room.clients.values()) {
        players.push({
            slot: client.slot,
            username: client.username,
            level: client.level,
            experience: client.experience,
            grunts_killed: client.gruntsKilled
        });
    }

    players.sort((a, b) => a.slot - b.slot);
    return players;
}

function broadcastRoom(room, payload) {
    for (const client of room.clients.values()) {
        safeSend(client.ws, payload);
    }
}

function broadcastLobbyState(room) {
    if (!room) {
        return;
    }

    broadcastRoom(room, {
        type: 'lobby_state',
        roomId: room.id,
        hostId: room.hostId,
        players: lobbyPlayers(room)
    });
}

function removeClientFromRoom(clientCtx, notify = true) {
    if (!clientCtx || !clientCtx.roomId) {
        return;
    }

    const room = rooms.get(clientCtx.roomId);
    if (!room) {
        clientCtx.roomId = null;
        return;
    }

    room.clients.delete(clientCtx.id);

    if (room.hostId === clientCtx.id) {
        const nextHost = room.clients.values().next();
        room.hostId = nextHost.done ? null : nextHost.value.id;
    }

    if (notify) {
        broadcastRoom(room, { type: 'player_left', clientId: clientCtx.id });
        broadcastLobbyState(room);
    }

    disposeRoomIfEmpty(room);
    clientCtx.roomId = null;
    clientCtx.slot = 0;
    clientCtx.state = null;
}

function joinRoom(clientCtx, message) {
    const roomId = normalizeRoomId(message.roomId);
    if (!roomId) {
        safeSend(clientCtx.ws, { type: 'error', error: 'RoomId requerido' });
        return;
    }

    if (clientCtx.roomId && clientCtx.roomId !== roomId) {
        removeClientFromRoom(clientCtx, true);
    }

    const room = getOrCreateRoom(roomId);

    if (!room.clients.has(clientCtx.id) && room.clients.size >= MAX_PLAYERS_PER_ROOM) {
        safeSend(clientCtx.ws, { type: 'error', error: 'La sala está llena (máximo 2 jugadores).' });
        return;
    }

    clientCtx.username = String(message.username || `Player_${clientCtx.id.slice(0, 4)}`).trim() || `Player_${clientCtx.id.slice(0, 4)}`;
    clientCtx.level = Number.isFinite(Number(message.level)) ? Number(message.level) : 1;
    clientCtx.experience = Number.isFinite(Number(message.experience)) ? Number(message.experience) : 0;
    clientCtx.gruntsKilled = Number.isFinite(Number(message.grunts_killed)) ? Number(message.grunts_killed) : 0;

    if (!room.clients.has(clientCtx.id)) {
        clientCtx.slot = assignSlot(room);
        if (clientCtx.slot < 0) {
            safeSend(clientCtx.ws, { type: 'error', error: 'No hay huecos disponibles en la sala.' });
            return;
        }

        room.clients.set(clientCtx.id, clientCtx);
    }

    if (!room.hostId) {
        room.hostId = clientCtx.id;
    }

    clientCtx.roomId = roomId;

    safeSend(clientCtx.ws, {
        type: 'welcome',
        clientId: clientCtx.id,
        roomId
    });

    broadcastLobbyState(room);
}

function startGame(clientCtx, message) {
    const room = clientCtx.roomId ? rooms.get(clientCtx.roomId) : null;
    if (!room) {
        safeSend(clientCtx.ws, { type: 'error', error: 'No estás en ninguna sala.' });
        return;
    }

    if (room.hostId !== clientCtx.id) {
        safeSend(clientCtx.ws, { type: 'error', error: 'Solo el host puede iniciar la partida.' });
        return;
    }

    if (room.clients.size < 2) {
        safeSend(clientCtx.ws, { type: 'error', error: 'Se necesitan 2 jugadores para iniciar la partida.' });
        return;
    }

    room.started = true;
    broadcastRoom(room, {
        type: 'start_game',
        scene: String(message.scene || 'SampleScene')
    });
}

function closeRoom(clientCtx) {
    const room = clientCtx.roomId ? rooms.get(clientCtx.roomId) : null;
    if (!room) {
        return;
    }

    if (room.hostId !== clientCtx.id) {
        safeSend(clientCtx.ws, { type: 'error', error: 'Solo el host puede cerrar la sala.' });
        return;
    }

    broadcastRoom(room, { type: 'error', error: 'La sala se ha cerrado.' });

    const clients = Array.from(room.clients.values());
    room.clients.clear();
    rooms.delete(room.id);

    for (const c of clients) {
        c.roomId = null;
        c.slot = 0;
        c.state = null;
    }
}

function joinMatch(clientCtx, message) {
    joinRoom(clientCtx, message);

    if (!clientCtx.roomId) {
        return;
    }

    const room = rooms.get(clientCtx.roomId);
    if (!room) {
        return;
    }

    room.started = true;
}

function broadcastMatchState(room) {
    const players = [];
    for (const client of room.clients.values()) {
        if (!client.state) {
            continue;
        }

        players.push({
            clientId: client.id,
            username: client.username,
            x: client.state.x,
            y: client.state.y,
            vx: client.state.vx,
            vy: client.state.vy,
            facingRight: client.state.facingRight,
            running: client.state.running,
            dead: client.state.dead
        });
    }

    broadcastRoom(room, {
        type: 'match_state',
        roomId: room.id,
        players
    });
}

function handlePlayerState(clientCtx, message) {
    const room = clientCtx.roomId ? rooms.get(clientCtx.roomId) : null;
    if (!room) {
        return;
    }

    const source = message.player || message;
    clientCtx.state = {
        x: Number(source.x) || 0,
        y: Number(source.y) || 0,
        vx: Number(source.vx) || 0,
        vy: Number(source.vy) || 0,
        facingRight: Boolean(source.facingRight),
        running: Boolean(source.running),
        dead: Boolean(source.dead)
    };

    broadcastMatchState(room);
}

const server = http.createServer(app);
const wss = new WebSocketServer({ noServer: true });

server.on('upgrade', (request, socket, head) => {
    const host = request.headers.host || 'localhost';
    const url = new URL(request.url || '/', `http://${host}`);

    if (url.pathname !== '/ws' && url.pathname !== '/') {
        socket.destroy();
        return;
    }

    wss.handleUpgrade(request, socket, head, (ws) => {
        wss.emit('connection', ws, request);
    });
});

wss.on('connection', (ws) => {
    const clientCtx = {
        id: createClientId(),
        ws,
        roomId: null,
        username: '',
        level: 1,
        experience: 0,
        gruntsKilled: 0,
        slot: 0,
        state: null
    };

    safeSend(ws, {
        type: 'welcome',
        clientId: clientCtx.id,
        roomId: ''
    });

    ws.on('message', (rawData) => {
        let message;
        try {
            message = JSON.parse(String(rawData || '{}'));
        } catch (_error) {
            safeSend(ws, { type: 'error', error: 'JSON inválido.' });
            return;
        }

        const type = String(message.type || '').trim();
        if (!type) {
            safeSend(ws, { type: 'error', error: 'Tipo de mensaje requerido.' });
            return;
        }

        if (type === 'join') {
            joinRoom(clientCtx, message);
            return;
        }

        if (type === 'start_game') {
            startGame(clientCtx, message);
            return;
        }

        if (type === 'close_room') {
            closeRoom(clientCtx);
            return;
        }

        if (type === 'join_match') {
            joinMatch(clientCtx, message);
            return;
        }

        if (type === 'player_state') {
            handlePlayerState(clientCtx, message);
            return;
        }

        safeSend(ws, { type: 'error', error: `Tipo de mensaje no soportado: ${type}` });
    });

    ws.on('close', () => {
        removeClientFromRoom(clientCtx, true);
    });

    ws.on('error', () => {
        removeClientFromRoom(clientCtx, true);
    });
});

const heartbeat = setInterval(() => {
    for (const client of wss.clients) {
        if (client.readyState === 1) {
            try {
                client.ping();
            } catch (_error) {
                // Ignore.
            }
        }
    }
}, HEARTBEAT_INTERVAL_MS);

wss.on('close', () => {
    clearInterval(heartbeat);
});

server.listen(PORT, () => {
    console.log(`Backend listening on port ${PORT}`);
});
