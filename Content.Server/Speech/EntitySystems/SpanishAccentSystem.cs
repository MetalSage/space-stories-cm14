using System.Text;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems
{
    public sealed class SpanishAccentSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<SpanishAccentComponent, AccentGetEvent>(OnAccent);
        }

        public string Accentuate(string message)
        {
            // Удваеваем "р" в сообщении с шансом в 40%
            message = DoubleLetterR(message);
            // If a sentence ends with ?, insert a reverse ? at the beginning of the sentence
            message = ReplacePunctuation(message);
            return message;
        }

        private string DoubleLetterR(string message)
        {
            // Создаем новый StringBuilder для формирования результата
            var msg = new System.Text.StringBuilder();

            for (int i = 0; i < message.Length; i++)
            {
                // Проверяем, является ли текущий символ 'р'
                if (message[i] == 'р')
                {
                    // Убедимся, что 'р' не первая, не последняя и не окружена пробелами
                    if (i > 0 && i < message.Length - 1 && message[i - 1] != ' ' && message[i + 1] != ' ')
                    {
                        if (_random.Next(100) < 40)
                        {
                            msg.Append('р'); // Удваиваем букву 'р'
                        }
                    }
                }
                else if (message[i] == 'Р')
                {

                    if (i > 0 && i < message.Length - 1 && message[i - 1] != ' ' && message[i + 1] != ' ')
                    {
                        if (_random.Next(100) < 40)
                        {
                            msg.Append('Р');
                        }
                    }
                }
                // Добавляем текущий символ в результат
                msg.Append(message[i]);
            }

            return msg.ToString();
        }

        private string ReplacePunctuation(string message)
        {
            var sentences = AccentSystem.SentenceRegex.Split(message);
            var msg = new StringBuilder();
            foreach (var s in sentences)
            {
                var toInsert = new StringBuilder();
                for (var i = s.Length - 1; i >= 0 && "?!‽".Contains(s[i]); i--)
                {
                    toInsert.Append(s[i] switch
                    {
                        '?' => '¿',
                        '!' => '¡',
                        '‽' => '⸘',
                        _ => ' '
                    });
                }
                if (toInsert.Length == 0)
                {
                    msg.Append(s);
                } else
                {
                    msg.Append(s.Insert(s.Length - s.TrimStart().Length, toInsert.ToString()));
                }
            }
            return msg.ToString();
        }

        private void OnAccent(EntityUid uid, SpanishAccentComponent component, AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message);
        }
    }
}
