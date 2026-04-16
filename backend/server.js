const express = require('express');
const sqlite3 = require('sqlite3').verbose();
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const cors = require('cors');

const app = express();
const PORT = 3000;
const SECRET_KEY = 'miniheroes_super_secret_key'; // En producción esto debería estar en un archivo .env

// Middleware
app.use(cors());
app.use(express.json()); // Permite recibir JSON en el body

// Base de datos SQLite
const db = new sqlite3.Database('./database.sqlite', (err) => {
    if (err) {
        console.error('Error conectando a la base de datos:', err.message);
    } else {
        console.log('Conectado a la base de datos SQLite.');
        // Crear tabla de usuarios si no existe
        db.run(`CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT UNIQUE,
            password TEXT
        )`, () => {
            // Modificaciones por si la tabla ya existía de antes
            db.run(`ALTER TABLE users ADD COLUMN experience INTEGER DEFAULT 0`, (err) => {});
            db.run(`ALTER TABLE users ADD COLUMN grunts_killed INTEGER DEFAULT 0`, (err) => {});
            db.run(`ALTER TABLE users ADD COLUMN level INTEGER DEFAULT 1`, (err) => {});
        });
    }
});

// Endpoint para REGISTRAR una cuenta
app.post('/api/register', async (req, res) => {
    const { username, password } = req.body;
    if (!username || !password) return res.status(400).json({ error: 'Falta username o password' });

    try {
        // Encriptar contraseña para guardarla de forma segura en la BD
        const hashedPassword = await bcrypt.hash(password, 10);
        
        // Insertar usuario en la BD
        const sql = 'INSERT INTO users (username, password) VALUES (?, ?)';
        db.run(sql, [username, hashedPassword], function(err) {
            if (err) {
                if (err.message.includes('UNIQUE')) {
                    return res.status(400).json({ error: 'El usuario ya existe' });
                }
                return res.status(500).json({ error: 'Error en la base de datos' });
            }
            res.status(201).json({ message: 'Usuario registrado exitosamente', userId: this.lastID });
        });
    } catch (err) {
        res.status(500).json({ error: 'Error interno del servidor' });
    }
});

// Endpoint para HACER LOGIN y obtener sesión
app.post('/api/login', (req, res) => {
    const { username, password } = req.body;
    if (!username || !password) return res.status(400).json({ error: 'Falta username o password' });

    const sql = 'SELECT * FROM users WHERE username = ?';
    db.get(sql, [username], async (err, user) => {
        if (err) return res.status(500).json({ error: 'Error en la base de datos' });
        if (!user) return res.status(401).json({ error: 'Credenciales inválidas' });

        // Comparar contraseña recibida con la contraseña encriptada guardada
        const isMatch = await bcrypt.compare(password, user.password);
        if (!isMatch) return res.status(401).json({ error: 'Credenciales inválidas' });

        // Crear token de sesión (JWT) que expira en 24 horas
        const token = jwt.sign({ id: user.id, username: user.username }, SECRET_KEY, { expiresIn: '24h' });
        
        res.json({ message: 'Login exitoso', token, username: user.username });
    });
});

// Middleware para verificar la sesión (Token) en rutas protegidas
const verifyToken = (req, res, next) => {
    const authHeader = req.headers['authorization'];
    // El token viene en el formato: "Bearer TOKEN"
    const token = authHeader && authHeader.split(' ')[1]; 

    if (!token) return res.status(403).json({ error: 'Se requiere un token de sesión para esto' });

    jwt.verify(token, SECRET_KEY, (err, decoded) => {
        if (err) return res.status(401).json({ error: 'Token de sesión inválido o expirado' });
        req.user = decoded; // Guardar los datos de la sesión disponibles para el endpoint
        next();
    });
};

// Endpoint que requiere iniciar sesión (Obtener la información de mi cuenta)
app.get('/api/me', verifyToken, (req, res) => {
    // Si se llega a ejecutar esto, significa que el token era válido y es de ese usuario
    res.json({ message: 'Sesión confirmada', user: req.user });
});

// Obtener estadísticas guardadas
app.get('/api/stats', verifyToken, (req, res) => {
    db.get('SELECT experience, grunts_killed, level FROM users WHERE id = ?', [req.user.id], (err, row) => {
        if (err) return res.status(500).json({ error: 'Error en la base de datos' });
        res.json(row || { experience: 0, grunts_killed: 0, level: 1 });
    });
});

// Guardar/Actualizar estadísticas
app.post('/api/stats', verifyToken, (req, res) => {
    const { experience, grunts_killed, level } = req.body;
    db.run(
        'UPDATE users SET experience = ?, grunts_killed = ?, level = ? WHERE id = ?',
        [experience || 0, grunts_killed || 0, level || 1, req.user.id],
        function(err) {
            if (err) return res.status(500).json({ error: 'Error en la base de datos al guardar' });
            res.json({ message: 'Estadísticas guardadas con éxito', experience, grunts_killed, level });
        }
    );
});

// Arrancar servidor
app.listen(PORT, () => {
    console.log(`Servidor Backend corriendo en http://localhost:${PORT}`);
});
