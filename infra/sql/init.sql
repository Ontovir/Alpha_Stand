-- Создание таблиц для loan-analytics-platform
-- Выполняется автоматически при первом старте PostgreSQL

CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS loans (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id),
    amount NUMERIC(15, 2) NOT NULL CHECK (amount > 0),
    status TEXT NOT NULL DEFAULT 'pending',
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS analytics_loans (
    id SERIAL PRIMARY KEY,
    loan_id INTEGER NOT NULL UNIQUE, -- Защита от дублей (идемпотентность)
    processed_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT fk_loan FOREIGN KEY (loan_id) REFERENCES loans(id)
);

-- Индексы для производительности
CREATE INDEX idx_loans_user_id ON loans(user_id);
CREATE INDEX idx_loans_status ON loans(status);
CREATE INDEX idx_analytics_processed_at ON analytics_loans(processed_at);

-- Тестовые данные (для начала 2 пользователя)
INSERT INTO users (name, email) VALUES 
    ('Иван Иванов', 'ivan@example.com'),
    ('Мария Петрова', 'maria@example.com')
ON CONFLICT (email) DO NOTHING;