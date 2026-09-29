alter table character add column nickname varchar(20);

update character
set nickname = left(account.email, greatest(1, least(20, position('@' in account.email) - 1)))
from account
where character.account_id = account.id
  and character.nickname is null;

alter table character alter column nickname set not null;
alter table character add constraint character_nickname_not_blank check (char_length(btrim(nickname)) between 1 and 20);
